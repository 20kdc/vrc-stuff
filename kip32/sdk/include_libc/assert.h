/*
 * kip32 minimal incomplete libc
 * This one's special and isn't eligible for once-pragma.
 */

/*
 * glibc and musl have both informally standardized on this exact function signature.
 * While we make implementing the function the application's responsibility, we keep the basic signature.
 * However, to avoid debug messing with codegen, and more importantly to ease implementation, we don't have _Noreturn here.
 */
void __assert_fail(const char * exprtext, const char * file, int line, const char * func);

#ifdef assert
#undef assert
#endif

#ifdef NDEBUG
#define assert(ignore) ((void)0)
#else
#define assert(expression) ((expression) ? ((void)0) : __assert_fail(#expression, __FILE__, __LINE__, __func__))
#endif

#ifndef static_assert
#define static_assert _Static_assert
#endif
