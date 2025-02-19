#pragma once
#include <string>
#include <vector>
#include <regex>
#include "method_info.h"

class InstrumentationFilter
{
public:
    static bool ShouldInstrumentAssembly(const std::wstring &assemblyName)
    {
        // Allow system assemblies
        if (assemblyName.find(L"System.") == 0)
            return true;
        if (assemblyName.find(L"Microsoft.") == 0)
            return true;
        return true; // Or your specific logic
    }

    static bool ShouldInstrumentMethod(const MethodInfo &methodInfo)
    {
        // Skip certain methods
        if (methodInfo.methodName.find(L".ctor") != std::wstring::npos)
            return false;
        if (methodInfo.methodName.find(L".cctor") != std::wstring::npos)
            return false;

        // Your specific rules here
        return true;
    }
};
