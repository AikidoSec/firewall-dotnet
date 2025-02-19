#pragma once
#if defined(_MSC_VER)
#include <intrin.h>
#endif
#include <atomic>

class PlatformOps
{
public:
    static inline void MemoryBarrier()
    {
#if defined(_M_ARM) || defined(_M_ARM64)
        __dmb(0xB); // Data Memory Barrier
#elif defined(_M_IX86) || defined(_M_X64)
        _ReadWriteBarrier();
#else
        std::atomic_thread_fence(std::memory_order_seq_cst);
#endif
    }

    static inline void MemoryCopy(void *dest, const void *src, size_t count)
    {
#if defined(_M_ARM) || defined(_M_ARM64)
        memcpy(dest, src, count); // Use standard memcpy for ARM
#elif defined(_M_IX86) || defined(_M_X64)
        __stosb((unsigned char *)dest, *(const unsigned char *)src, count);
#else
        memcpy(dest, src, count);
#endif
    }

    static inline long InterlockedIncrement(long *value)
    {
#if defined(_M_ARM) || defined(_M_ARM64)
        return _InterlockedIncrement_acq(value);
#else
        return _InterlockedIncrement(value);
#endif
    }

    static inline long InterlockedDecrement(long *value)
    {
#if defined(_M_ARM) || defined(_M_ARM64)
        return _InterlockedDecrement_rel(value);
#else
        return _InterlockedDecrement(value);
#endif
    }
};
