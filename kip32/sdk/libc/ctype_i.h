#pragma once

/*
 * Internal ctype, includes macros
 */

/* root character classes */

#define ISUPPER(c) (((c) >= 'A') && ((c) <= 'Z'))
#define ISLOWER(c) (((c) >= 'a') && ((c) <= 'z'))
#define ISDIGIT(c) (((c) >= '0') && ((c) <= '9'))
#define ISBLANK(c) (((c) == ' ') || ((c) == '\t'))
/* note that the flag encoding assumes that within ASCII, PRINT = !CNTRL */
#define ISPRINT(c) (((c) >= 0x20) && ((c) <= 0x7E))
#define ISCNTRL(c) ((((c) >= 0x00) && ((c) <= 0x1F)) || ((c) == 0x7F))
#define ISSPACE(c) (((c) == ' ') || ((c) == '\f') || ((c) == '\n') || ((c) == '\r') || ((c) == '\t') || ((c) == '\v'))

#define ISASCII(c) (((unsigned) (c)) < 128)

/* composite character classes */

#define ISXDIGIT(c) (ISDIGIT(c) || \
	(((c) >= 'A') && ((c) <= 'F')) || \
	(((c) >= 'a') && ((c) <= 'f')) \
)

#define ISALPHA(c) (ISLOWER(c) || ISUPPER(c))
#define ISALNUM(c) (ISALPHA(c) || ISDIGIT(c))
#define ISGRAPH(c) (ISPRINT(c) && ((c) != ' '))
#define ISPUNCT(c) (ISPRINT(c) && !(ISSPACE(c) || ISALNUM(c)))
