#include <ctype.h>
#include "ctype_i.h"

#define CT_FLAG(p, v) ((p) ? v : 0)

#define F_UPPER 0x01
#define F_LOWER 0x02
#define F_DIGIT 0x04
#define F_BLANK 0x08
#define F_PRINT 0x10
#define F_SPACE 0x20
#define F_XDIGI 0x40
#define F_GRAPH 0x80

#define CT_FLAGS(c) ( \
	CT_FLAG(ISUPPER(c), F_UPPER) | \
	CT_FLAG(ISLOWER(c), F_LOWER) | \
	CT_FLAG(ISDIGIT(c), F_DIGIT) | \
	CT_FLAG(ISBLANK(c), F_BLANK) | \
	CT_FLAG(ISPRINT(c), F_PRINT) | \
	CT_FLAG(ISSPACE(c), F_SPACE) | \
	CT_FLAG(ISXDIGIT(c), F_XDIGI) | \
	CT_FLAG(ISGRAPH(c), F_GRAPH) \
)

/* for i = 0, 127 do io.write("CT_FLAGS(" .. tostring(i) .. "),") end print() */
static const unsigned char flags[128] = {CT_FLAGS(0),CT_FLAGS(1),CT_FLAGS(2),CT_FLAGS(3),CT_FLAGS(4),CT_FLAGS(5),CT_FLAGS(6),CT_FLAGS(7),CT_FLAGS(8),CT_FLAGS(9),CT_FLAGS(10),CT_FLAGS(11),CT_FLAGS(12),CT_FLAGS(13),CT_FLAGS(14),CT_FLAGS(15),CT_FLAGS(16),CT_FLAGS(17),CT_FLAGS(18),CT_FLAGS(19),CT_FLAGS(20),CT_FLAGS(21),CT_FLAGS(22),CT_FLAGS(23),CT_FLAGS(24),CT_FLAGS(25),CT_FLAGS(26),CT_FLAGS(27),CT_FLAGS(28),CT_FLAGS(29),CT_FLAGS(30),CT_FLAGS(31),CT_FLAGS(32),CT_FLAGS(33),CT_FLAGS(34),CT_FLAGS(35),CT_FLAGS(36),CT_FLAGS(37),CT_FLAGS(38),CT_FLAGS(39),CT_FLAGS(40),CT_FLAGS(41),CT_FLAGS(42),CT_FLAGS(43),CT_FLAGS(44),CT_FLAGS(45),CT_FLAGS(46),CT_FLAGS(47),CT_FLAGS(48),CT_FLAGS(49),CT_FLAGS(50),CT_FLAGS(51),CT_FLAGS(52),CT_FLAGS(53),CT_FLAGS(54),CT_FLAGS(55),CT_FLAGS(56),CT_FLAGS(57),CT_FLAGS(58),CT_FLAGS(59),CT_FLAGS(60),CT_FLAGS(61),CT_FLAGS(62),CT_FLAGS(63),CT_FLAGS(64),CT_FLAGS(65),CT_FLAGS(66),CT_FLAGS(67),CT_FLAGS(68),CT_FLAGS(69),CT_FLAGS(70),CT_FLAGS(71),CT_FLAGS(72),CT_FLAGS(73),CT_FLAGS(74),CT_FLAGS(75),CT_FLAGS(76),CT_FLAGS(77),CT_FLAGS(78),CT_FLAGS(79),CT_FLAGS(80),CT_FLAGS(81),CT_FLAGS(82),CT_FLAGS(83),CT_FLAGS(84),CT_FLAGS(85),CT_FLAGS(86),CT_FLAGS(87),CT_FLAGS(88),CT_FLAGS(89),CT_FLAGS(90),CT_FLAGS(91),CT_FLAGS(92),CT_FLAGS(93),CT_FLAGS(94),CT_FLAGS(95),CT_FLAGS(96),CT_FLAGS(97),CT_FLAGS(98),CT_FLAGS(99),CT_FLAGS(100),CT_FLAGS(101),CT_FLAGS(102),CT_FLAGS(103),CT_FLAGS(104),CT_FLAGS(105),CT_FLAGS(106),CT_FLAGS(107),CT_FLAGS(108),CT_FLAGS(109),CT_FLAGS(110),CT_FLAGS(111),CT_FLAGS(112),CT_FLAGS(113),CT_FLAGS(114),CT_FLAGS(115),CT_FLAGS(116),CT_FLAGS(117),CT_FLAGS(118),CT_FLAGS(119),CT_FLAGS(120),CT_FLAGS(121),CT_FLAGS(122),CT_FLAGS(123),CT_FLAGS(124),CT_FLAGS(125),CT_FLAGS(126),CT_FLAGS(127),};

/* trivial enough that table lookups aren't worth it */

int isupper(int c) {
	return ISUPPER(c);
}

int islower(int c) {
	return ISLOWER(c);
}

int isdigit(int c) {
	return ISDIGIT(c);
}

int isblank(int c) {
	return ISBLANK(c);
}

int isprint(int c) {
	return ISPRINT(c);
}

/* now actually worth using tables */

int iscntrl(int c) {
	if (!ISASCII(c))
		return 0;
	return !(flags[c] & F_PRINT);
}

int isspace(int c) {
	if (!ISASCII(c))
		return 0;
	return flags[c] & F_SPACE;
}

int isxdigit(int c) {
	if (!ISASCII(c))
		return 0;
	return flags[c] & F_XDIGI;
}

int isalnum(int c) {
	if (!ISASCII(c))
		return 0;
	return flags[c] & (F_UPPER | F_LOWER | F_DIGIT);
}

int isalpha(int c) {
	if (!ISASCII(c))
		return 0;
	return flags[c] & (F_UPPER | F_LOWER);
}

int isgraph(int c) {
	if (!ISASCII(c))
		return 0;
	return flags[c] & F_GRAPH;
}

int ispunct(int c) {
	if (!ISASCII(c))
		return 0;
	/* (ISPRINT(c) && !(ISSPACE(c) || ISALNUM(c))) */
	return (flags[c] & (F_PRINT | F_SPACE | F_UPPER | F_LOWER | F_DIGIT)) == F_PRINT;
}

/* and these two */

int tolower(int c) {
	return ISUPPER(c) ? ((c - 'A') + 'a') : 0;
}

int toupper(int c) {
	return ISLOWER(c) ? ((c - 'a') + 'A') : 0;
}
