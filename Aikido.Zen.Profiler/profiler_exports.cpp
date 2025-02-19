#include "profiler_exports.h"
#include "aikido_profiler.h"
#include "platform_ops.h"
#include <stdexcept>

// Global profiler instance - defined in aikido_profiler.cpp
extern AikidoProfiler *g_profiler;

AIKIDO_EXPORT HRESULT RequestReJIT(const wchar_t *assemblyName, const wchar_t *methodName)
{
    PlatformOps::MemoryBarrier(); // Use platform-agnostic memory barrier
    if (auto instance = AikidoProfiler::GetInstance())
    {
        return instance->RequestReJIT(assemblyName, methodName);
    }
    return E_FAIL;
}

AIKIDO_EXPORT HRESULT RemoveMethodToInstrument(const wchar_t *assemblyName, const wchar_t *methodName)
{
    PlatformOps::MemoryBarrier(); // Use platform-agnostic memory barrier
    if (auto instance = AikidoProfiler::GetInstance())
    {
        instance->RemoveMethodToInstrument(assemblyName, methodName);
        return S_OK;
    }
    return E_FAIL;
}
