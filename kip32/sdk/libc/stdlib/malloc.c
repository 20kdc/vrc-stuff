/*
 * kip32 malloc
 */

#include <stddef.h>
#include <stdint.h>
#include <stdlib.h>
#include <unistd.h>
#include <limits.h>
#include <string.h>
#include <assert.h>

/* To maintain alignment, the malloc works in 'words' internally. */
typedef unsigned long long dword;
#define WORD_ALIGNMASK (sizeof(uintptr_t) - 1)

/* debug switches */
/*
#define MALLOC_DBG_NONAV
#define MALLOC_DBG_REALLOC_NOMERGE
#define MALLOC_DBG_NORECLAIM
#define MALLOC_DBG_PARANOID
#define MALLOC_DBG_NOISY system
*/
#define MALLOC_DBG_PARANOID

/* Minimum amount of data in words that will be requested via sbrk. */
#define MALLOC_SLABSIZE 0x4000
/*
 * If at least this amount of words is sitting at the end of the heap in a free block, reduce the heap size.
 * Must be greater than MALLOC_SLABSIZE, as that is what is left free afterwards.
 */
#define MALLOC_RECLAIMSIZE 0x20000

/* block datatype */

/*
 * Both the block status and the next block pointer exist in the same bitfield without bit-shifting.
 * This is a careful and delicate construction. This section of the module mediates this. - 🟪
 */

typedef enum {
	blkState_Err0 = 0,
	blkState_Err1 = 1,
	blkState_Free = 2,
	blkState_Used = 3,
	blkState_Mask = 3
} blkstate_t;

static inline int baStateValid(uintptr_t v) {
	return (v == blkState_Free) || (v == blkState_Used);
}

typedef struct {
	uintptr_t controlWord;
	uintptr_t words[];
} block_t;

static inline blkstate_t baGetStatus(block_t * block) {
	uintptr_t v = block->controlWord & blkState_Mask;
#ifdef MALLOC_DBG_PARANOID
	assert(baStateValid(v));
#endif
	return v;
}

static inline void baSetStatus(block_t * block, blkstate_t status) {
#ifdef MALLOC_DBG_PARANOID
	assert(baStateValid(status));
#endif
	block->controlWord = (block->controlWord & ~blkState_Mask) | status;
}

static inline block_t * baGetNext(block_t * block) {
	return (block_t *) (block->controlWord & ~blkState_Mask);
}

static inline void baSetNext(block_t * block, block_t * nextBlock) {
	uintptr_t v = (uintptr_t) nextBlock;
#ifdef MALLOC_DBG_PARANOID
	assert(!(v & blkState_Mask));
#endif
	block->controlWord = (block->controlWord & blkState_Mask) | v;
}

static inline void baSet(block_t * result, blkstate_t status, block_t * nextBlock) {
	uintptr_t v = (uintptr_t) nextBlock;
#ifdef MALLOC_DBG_PARANOID
	assert(baStateValid(status));
	assert(!(v & blkState_Mask));
#endif
	v |= status;
	result->controlWord = v;
}

/* Pointer into content area by words. */
static inline void * baContentPtr(block_t * block, size_t word) {
	return &block->words[word];
}

/* Content area length in words for the given next block ; the area if block was merged until next. */
static inline size_t baContentLenHypothesis(block_t * block, block_t * next) {
	uintptr_t sz = (uintptr_t) next;
	sz -= (uintptr_t) block->words;
	sz /= sizeof(uintptr_t);
	return sz;
}

/* Content area length in words. */
static inline size_t baContentLen(block_t * block) {
	return baContentLenHypothesis(block, baGetNext(block));
}

/* Block from content. */
static inline block_t * baBlockFromContent(void * content) {
	return (block_t *) (content - offsetof(block_t, words));
}

/* other */

#define BYTES_TO_WORDS(v) (((v) + (sizeof(uintptr_t) - 1)) / sizeof(uintptr_t))

/* global variables */

/*
 * First heap block. No special properties, other than being a constant.
 * Presence is used as an indicator the heap has been setup.
 */
static block_t * heapStart = NULL;

/*
 * Heap end block. This points to an in-use block of length 0.
 * This block is special, because it's used to do two things:
 * 1. Terminate blockCollapse and other merging algorithms without an explicit end of heap check
 * 2. Recover from application uses of sbrk by marking the afflicted area as an eternally in-use block (effectively canonizing it as a malloc() allocation)
 */
static block_t * heapEnd = NULL;

/*
 * Expands (or initializes) the heap.
 * Note that 'words' is treated as the size in words of the desired resulting free block, which is returned directly.
 * It is certain that if a block is returned, it is a free block at least of the desired size.
 * Additional free memory may be created due to the potential removal of redundant heap end markers, but it isn't "certain" memory.
 */
static block_t * expandHeap(uintptr_t words) {
	/* If necessary, fix alignment. */
	uintptr_t alignmentChk = ((uintptr_t) sbrk(0)) & WORD_ALIGNMASK;
	if (alignmentChk)
		assert(sbrk(sizeof(uintptr_t) - alignmentChk) != ((void *) -1));
	/*
	 * Expand. We require room for:
	 * Free block header
	 * The free block contents (words)
	 * Heap end block header
	 */
	/* Try to allocate memory. */
	block_t * newram = (block_t *) sbrk(sizeof(block_t) + (words * sizeof(uintptr_t)) + sizeof(block_t));
	if (newram == ((void *) -1))
		return NULL;
	if (!heapStart) {
		/*
		 * heapStart did not presently exist, therefore there is no heapEnd.
		 */
		heapStart = newram;
	} else {
		/*
		 * Adjust existing heapEnd.
		 * If sbrk was used to allocate manually in the meantime, the resulting block incorporates that memory.
		 */
		int sbrkUsed = newram != baContentPtr(heapEnd, 0);
		baSet(heapEnd, sbrkUsed ? blkState_Used : blkState_Free, newram);
	}
	/*
	 * setup new blocks
	 */
	heapEnd = baContentPtr(newram, words);
	baSet(newram, blkState_Free, heapEnd);
	baSet(heapEnd, blkState_Used, heapEnd + 1);
	return newram;
}

/*
 * Makes a block the new heap end.
 * You promise that no application-used blocks lay after this block and that sbrk(0) == baContentPtr(heapEnd, 0).
 */
static inline void chopHeap(block_t * newHeapEnd) {
#ifdef MALLOC_DBG_PARANOID
	assert(sbrk(0) == baContentPtr(heapEnd, 0));
	assert(baGetNext(newHeapEnd) == heapEnd);
	assert(baGetStatus(newHeapEnd) == blkState_Free);
#endif
	intptr_t reclaimBytes = ((uintptr_t) baContentPtr(heapEnd, 0)) - ((uintptr_t) baContentPtr(newHeapEnd, 0));
	if (sbrk(-reclaimBytes) != (void *) -1) {
		/* Freeing successful; change over to new heap end. */
		baSet(newHeapEnd, blkState_Used, baContentPtr(newHeapEnd, 0));
		heapEnd = newHeapEnd;
	}
}

#if !(defined(MALLOC_DBG_NONAV) && defined(MALLOC_DBG_REALLOC_NOMERGE) && defined(MALLOC_DBG_NORECLAIM))
/* Collapse free blocks in the following chain. */
static int blockCollapse(block_t * block) {
	if (baGetStatus(block) == blkState_Used)
		return 0;
	int merged = 0;
	while (1) {
		block_t * nextBlock = baGetNext(block);
		assert(((uintptr_t) nextBlock) > ((uintptr_t) block));
		if (baGetStatus(nextBlock) == blkState_Used)
			break;
		/* Absorb block. */
		void * nextNextBlock = baGetNext(nextBlock);
		assert(((uintptr_t) nextNextBlock) > ((uintptr_t) nextBlock));
		baSetNext(block, baGetNext(nextBlock));
		merged++;
	}
	return merged;
}
#endif

/*
 * Splits a block given the block header, and the desired words.
 * The first half of the block is marked with the given state and the second is marked as free.
 * If existing == desired, the block is simply marked in-use.
 * Returns the content pointer for convenience.
 */
static inline void * setupBlock(block_t * block, blkstate_t state, size_t desiredWords) {
	/* due to only using one word of control data, this can always succeed */
	block_t * nextBlock = baGetNext(block);
	block_t * splitLocation = baContentPtr(block, desiredWords);
#ifdef MALLOC_DBG_PARANOID
	assert(baContentLen(block) >= desiredWords);
#endif
	/* If necessary, create new block header at split point. */
	if (nextBlock != splitLocation)
		baSet(splitLocation, blkState_Free, nextBlock);
	baSet(block, state, splitLocation);
	return baContentPtr(block, 0);
}

void * malloc(size_t size) {
	if (!heapStart) {
		/* initialize the heap to 64KiB */
		if (!expandHeap(MALLOC_SLABSIZE)) {
			/* can't! */
			return NULL;
		}
	}
	size_t desiredWords = BYTES_TO_WORDS(size);
	/* heap is now initialized */
#ifndef MALLOC_DBG_NONAV
	block_t * blockNav = heapStart;
	while (blockNav != heapEnd) {
		if (baGetStatus(blockNav) == blkState_Free) {
			/* try collapse */
			blockCollapse(blockNav);
			/* big enough? */
			size_t existingWords = baContentLen(blockNav);
			if (existingWords >= desiredWords)
				return setupBlock(blockNav, blkState_Used, desiredWords);
		}
		void * next = baGetNext(blockNav);
		/* Not marked as paranoid due to infinite loop risks. */
		assert(((uintptr_t) next) > ((uintptr_t) blockNav));
		blockNav = next;
	}
#endif
	/* ran out of heap! if < 64k, expand by that, else by whatever was asked for. */
	size_t makeWords = desiredWords;
	if (makeWords < MALLOC_SLABSIZE)
		makeWords = MALLOC_SLABSIZE;
	block_t * newram = expandHeap(makeWords);
	if (!newram)
		return NULL;
	return setupBlock(newram, blkState_Used, desiredWords);
}

void * realloc(void * ptr, size_t size) {
	if (ptr) {
		/* de-facto standard behaviour: free on zero */
		if (!size) {
			free(ptr);
			return NULL;
		}
#ifndef MALLOC_DBG_REALLOC_NOMERGE
		/* try expand? */
		block_t * ptrBlk = baBlockFromContent(ptr);
		assert(baGetStatus(ptrBlk) == blkState_Used);
		assert(ptrBlk != heapEnd);
		size_t desiredWords = BYTES_TO_WORDS(size);
		size_t existingWords = baContentLen(ptrBlk);
		if (existingWords < desiredWords) {
			/* look at the next block, and if not in use... */
			block_t * nextBlock = baGetNext(ptrBlk);
			if (baGetStatus(nextBlock) == blkState_Free) {
				blockCollapse(nextBlock);
				block_t * nextNextBlock = baGetNext(nextBlock);
				size_t mergedWords = baContentLenHypothesis(ptrBlk, nextNextBlock);
				/* Does merging provide enough words? */
				if (mergedWords >= desiredWords) {
					baSet(ptrBlk, blkState_Used, nextNextBlock);
					/* Signal to below code to chop the block. */
					existingWords = mergedWords;
				}
			}
		}
		/* if the allocation we have is 'good enough', use setupBlock and return the result */
		if (existingWords >= desiredWords)
			return setupBlock(ptrBlk, blkState_Used, desiredWords);
#endif
	}
	void * newContent = malloc(size);
	if (newContent && ptr) {
		/*
		 * transfer content and free existing ptr
		 * note that if we fail, ptr is *not* freed! this is by spec
		 */
		block_t * ptrBlk = baBlockFromContent(ptr);
		size_t transfer = baContentLen(ptrBlk) * sizeof(uintptr_t);
		if (transfer > size)
			transfer = size;
		memcpy(newContent, ptr, transfer);
		baSetStatus(ptrBlk, blkState_Free);
	}
	return newContent;
}

void free(void * ptr) {
	if (!ptr)
		return;
	block_t * ptrBlk = baBlockFromContent(ptr);
	assert(baGetStatus(ptrBlk) == blkState_Used);
	assert(ptrBlk != heapEnd);
	baSetStatus(ptrBlk, blkState_Free);
#ifndef MALLOC_DBG_NORECLAIM
	blockCollapse(ptrBlk);
	if ((baContentLen(ptrBlk) >= MALLOC_RECLAIMSIZE) && (baGetNext(ptrBlk) == heapEnd) && (sbrk(0) == baContentPtr(heapEnd, 0))) {
#ifdef MALLOC_DBG_NOISY
		MALLOC_DBG_NOISY("** RECLAIM **\n");
#endif
		setupBlock(ptrBlk, blkState_Free, MALLOC_SLABSIZE);
		chopHeap(baGetNext(ptrBlk));
	}
#endif
}

/* 'frontend' functions */

void * calloc(size_t nmemb, size_t size) {
	dword mul = ((dword) size) * ((dword) nmemb);
	if (mul > SIZE_MAX)
		return NULL;
	size_t sz = (size_t) mul;
	void * res = malloc(sz);
	if (res)
		memset(res, 0, sz);
	return res;
}
