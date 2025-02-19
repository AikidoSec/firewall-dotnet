#pragma once
#include <atomic>
#include <intrin.h>

#if defined(_M_ARM) || defined(_M_ARM64)
#include <arm_neon.h>
#include <arm64intr.h>
#endif

namespace Platform
{
    class Intrinsics
    {
    public:
        static inline long InterlockedIncrement(long *value)
        {
#if defined(_M_ARM) || defined(_M_ARM64)
            return (long)__atomic_add_fetch((volatile int *)value, 1, __ATOMIC_SEQ_CST);
#else
            return _InterlockedIncrement(value);
#endif
        }

        static inline long InterlockedDecrement(long *value)
        {
#if defined(_M_ARM) || defined(_M_ARM64)
            return (long)__atomic_sub_fetch((volatile int *)value, 1, __ATOMIC_SEQ_CST);
#else
            return _InterlockedDecrement(value);
#endif
        }

        static inline void MemoryBarrier()
        {
#if defined(_M_ARM) || defined(_M_ARM64)
            __dmb(_ARM64_BARRIER_ISH);
#else
            std::atomic_thread_fence(std::memory_order_seq_cst);
#endif
        }

        static inline void *MemoryCopy(void *dest, const void *src, size_t count)
        {
            return memcpy(dest, src, count);
        }
    };
}
