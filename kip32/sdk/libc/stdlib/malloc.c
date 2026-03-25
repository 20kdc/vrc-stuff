/*
 * kip32 malloc (NYI)
 */

#include <stddef.h>
#include <stdlib.h>
#include <unistd.h>
#include <limits.h>
#include <string.h>
#include <assert.h>

/* To maintain alognment, the malloc works in 'words'. */
typedef unsigned long word;
typedef unsigned long long dword;
#define WORD_MAX ULONG_MAX
#define WORD_HIGHBIT ((word) (LONG_MIN))
#define WORD_NHIGHBIT (~WORD_HIGHBIT)

#define HBS_FROM_PTR(v) ((((word *) (v)))[-1])
#define HBS_INUSE WORD_HIGHBIT
#define HBS_WORDSMASK WORD_NHIGHBIT
#define BYTES_TO_WORDS(v) (((v) + (sizeof(word) - 1)) / sizeof(word))

/* debug switches */
/*
#define MALLOC_DBG_NONAV
#define MALLOC_DBG_REALLOC_NOMERGE
*/
#define MALLOC_DBG_REALLOC_NOMERGE

static word * heapStart = NULL;
/*
 * Heap end block. This points to an in-use block of length 0.
 * This block is special, because it's used to do two things:
 * 1. Terminate blockCollapse and other merging algorithms without an explicit end of heap check
 * 2. Recover from application uses of sbrk by marking the afflicted area as an eternally in-use block (effectively canonizing it as a malloc() allocation)
 */
static word * heapEnd = NULL;

/*
 * Expands (or initializes) the heap.
 * Note that 'words' is treated as the size in words of the desired resulting free block, which is returned directly.
 * It is certain that if a block is returned, it is a free block at least of the desired size.
 * Additional free memory is created due to the potential removal of redundant heap end markers, but it isn't "certain" memory.
 */
static word * expandHeap(word words) {
	/* If necessary, fix alignment. */
	word alignmentChk = ((word) sbrk(0)) & (sizeof(word) - 1);
	if (alignmentChk)
		assert(sbrk(sizeof(word) - alignmentChk) != ((void *) -1));
	/* Expand */
	words += 2;
	/* Try to allocate memory. */
	word * newram = (word *) sbrk(words * sizeof(word));
	if (newram == ((void *) -1))
		return NULL;
	if (!heapStart) {
		heapStart = newram;
	} else if (newram != (heapEnd + 1)) {
		/* Someone sbrk'd, fix up their mess. */
		size_t sbrkRange = newram - (heapEnd + 1);
		heapEnd[0] = HBS_INUSE | sbrkRange;
	} else {
		/* Free existing heapEnd. */
		heapEnd[0] = 0;
	}
	/* setup new blocks */
	newram[0] = words - 2;
	heapEnd = newram + (words - 1);
	heapEnd[0] = HBS_INUSE;
	return newram;
}

/* Collapse free blocks in the following chain. */
static int blockCollapse(word * blockStatusWord) {
	word w = blockStatusWord[0];
	if (w & HBS_INUSE)
		return 0;
	int merged = 0;
	while (1) {
		word words = w & HBS_WORDSMASK;
		word nextBlockStatus = blockStatusWord[words];
		if (nextBlockStatus & HBS_INUSE)
			break;
		/* not in use? well don't mind if I do then */
		w += 1 + (nextBlockStatus & HBS_WORDSMASK);
		merged++;
	}
	blockStatusWord[0] = w;
	return merged;
}

/*
 * Splits a block given the content pointer as a word pointer (ptrW), and the desired words.
 * The first half of the block is marked as used and the second is marked as free.
 * If existing == desired, the block is simply marked in-use.
 * As it is usually desired to return ptrW when this is called, that is done for tail-return
 */
static inline void * setupBlock(word * ptrW, word desiredWords) {
	/*
	 * due to only using one word of control data, this can always succeed
	 * layout is:
	 * -1: in use, DW words
	 * 0..: content[DW]: original block
	 * DW: free, EW - (DW + 1) words
	 * DW+1..: content[EW - (DW + 1)]: free
	 * EW: (next block)
	 */
	word existingWords = ptrW[-1] & HBS_WORDSMASK;
	assert(existingWords >= desiredWords);
	assert((desiredWords & HBS_WORDSMASK) == desiredWords);
	if (existingWords > desiredWords)
		ptrW[desiredWords] = existingWords - (desiredWords + 1);
	ptrW[-1] = HBS_INUSE | desiredWords;
	return ptrW;
}

void * malloc(size_t size) {
	if (!heapStart) {
		/* initialize the heap to 64KiB */
		if (!expandHeap(0x4000)) {
			/* can't! */
			return NULL;
		}
	}
	word desiredWords = BYTES_TO_WORDS(size);
	/* heap is now initialized */
#ifndef MALLOC_DBG_NONAV
	word * blockNav = heapStart;
	while (blockNav != heapEnd) {
		if (!(blockNav[0] & HBS_INUSE)) {
			/* try collapse */
			blockCollapse(blockNav);
			/* big enough? */
			word existingWords = blockNav[0] & HBS_WORDSMASK;
			if (existingWords >= desiredWords) {
				word * ptrW = blockNav + 1;
				return setupBlock(ptrW, desiredWords);
			}
		}
		blockNav += 1 + (blockNav[0] & HBS_WORDSMASK);
	}
#endif
	/* ran out of heap! if < 64k, expand by that, else by whatever was asked for. */
	size_t makeWords = desiredWords;
	if (makeWords < 0x4000)
		makeWords = 0x4000;
	word * newram = expandHeap(makeWords);
	if (!newram)
		return NULL;
	return setupBlock(newram + 1, desiredWords);
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
		word * ptrW = (word *) ptr;
		word desiredWords = BYTES_TO_WORDS(size);
		word existingWords = ptrW[-1] & HBS_WORDSMASK;
		int ok = 0;
		if (existingWords < desiredWords) {
			/* look at the next block, and if not in use... */
			blockCollapse(ptrW + existingWords);
			word nextBlockInfo = ptrW[existingWords];
			word mergedWords = existingWords + 1 + (nextBlockInfo & HBS_WORDSMASK);
			if ((!(nextBlockInfo & HBS_INUSE)) && (mergedWords >= desiredWords)) {
				/* Not in use and meets requirements -- OK for merge */
				ptrW[-1] = HBS_INUSE | mergedWords;
				/* so that we split again if needed */
				existingWords = mergedWords;
			}
		}
		/* if the allocation we have is 'good enough', use setupBlock and return the result */
		if (existingWords >= desiredWords)
			return setupBlock(ptrW, desiredWords);
#endif
	}
	void * newBlock = malloc(size);
	if (newBlock && ptr) {
		/*
		 * transfer content and free existing ptr
		 * note that if we fail, ptr is *not* freed! this is by spec
		 */
		word ptrstatus = HBS_FROM_PTR(ptr);
		size_t transfer = (ptrstatus & HBS_WORDSMASK) * sizeof(word);
		if (transfer > size)
			transfer = size;
		memcpy(newBlock, ptr, transfer);
		ptrstatus &= ~HBS_INUSE;
		HBS_FROM_PTR(ptr) = ptrstatus;
	}
	return newBlock;
}

void free(void * ptr) {
	if (!ptr)
		return;
	assert(HBS_FROM_PTR(ptr) & HBS_INUSE);
	HBS_FROM_PTR(ptr) &= ~HBS_INUSE;
}

/* 'frontend' functions */

void * calloc(size_t nmemb, size_t size) {
	dword mul = ((dword) size) * ((dword) nmemb);
	if (mul > WORD_MAX)
		return NULL;
	word sz = (word) mul;
	void * res = malloc(sz);
	if (res)
		memset(res, 0, sz);
	return res;
}
