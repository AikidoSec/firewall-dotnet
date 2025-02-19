#pragma once
#include <cor.h>

// Add ARM-specific headers and definitions
#if defined(_M_ARM) || defined(_M_ARM64)
#include <armintr.h>
#include <intrin.h>

// Define missing intrinsics for ARM
#define _ReadWriteBarrier() __dmb(0xB) // Data Memory Barrier
#define _InterlockedIncrement(p) _InterlockedIncrement_acq(p)
#define _InterlockedDecrement(p) _InterlockedDecrement_rel(p)
#define _InterlockedExchange(p, v) _InterlockedExchange_acq(p, v)
#define _InterlockedCompareExchange(p, v, c) _InterlockedCompareExchange_acq(p, v, c)
#define __stosb(d, v, c) __builtin_memset(d, v, c)
#endif

#ifdef _WIN32
#define AIKIDO_EXPORT extern "C" __declspec(dllexport)
#else
#define AIKIDO_EXPORT extern "C" __attribute__((visibility("default")))
#endif

// Function declarations for profiler exports
AIKIDO_EXPORT HRESULT RequestReJIT(const wchar_t *assemblyName, const wchar_t *methodName);
AIKIDO_EXPORT HRESULT RemoveMethodToInstrument(const wchar_t *assemblyName, const wchar_t *methodName);

#ifdef _WIN32
BOOL WINAPI DllMain(HMODULE hModule, DWORD reason, LPVOID reserved)
{
    return TRUE;
}
#endif
