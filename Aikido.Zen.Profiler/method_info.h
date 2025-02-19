#pragma once
#include <string>
#include <vector>
#include <cor.h>
#include <corhdr.h>
#include <corprof.h>

struct TypeInfo
{
    mdToken typeToken;
    bool isValueType;
    std::wstring typeName;
};

class MethodInfo
{
public:
    ModuleID moduleId;
    mdToken methodToken;
    mdToken typeToken;
    std::wstring methodName;
    std::wstring className;
    std::wstring assemblyName;
    DWORD attributes;
    bool isStatic;
    bool isPublic;
    bool isPrivate;
    bool isVirtual;
    bool isValueType;
    ULONG argCount;
    std::vector<TypeInfo> argTypes;
    BYTE *ilCode;
    ULONG codeSize;

    MethodInfo()
        : moduleId(0), methodToken(0), typeToken(0),
          attributes(0), isStatic(false), isPublic(false),
          isPrivate(false), isVirtual(false), isValueType(false),
          argCount(0), ilCode(nullptr), codeSize(0) {}

    std::wstring GetFullName() const
    {
        return assemblyName + L"!" + className + L"." + methodName;
    }

    bool IsConstructor() const
    {
        return methodName.find(L".ctor") != std::wstring::npos ||
               methodName.find(L".cctor") != std::wstring::npos;
    }

    static MethodInfo FromFunctionInfo(ICorProfilerInfo4 *profilerInfo, FunctionID functionId)
    {
        MethodInfo info;
        ClassID classId;
        ModuleID moduleId;
        mdToken token;

        if (FAILED(profilerInfo->GetFunctionInfo(functionId, &classId, &moduleId, &token)))
            return info;

        info.moduleId = moduleId;
        info.methodToken = token;

        IMetaDataImport *metaData = nullptr;
        if (FAILED(profilerInfo->GetModuleMetaData(moduleId, ofRead, IID_IMetaDataImport, (IUnknown **)&metaData)))
            return info;

        // Get method properties
        mdTypeDef typeDef;
        WCHAR methodName[1024];
        WCHAR className[1024];
        DWORD methodAttr;
        PCCOR_SIGNATURE sigBlob;
        ULONG sigBlobLength;
        ULONG nameLength;
        ULONG codeRva;

        HRESULT hr = metaData->GetMethodProps(
            token,
            &typeDef,
            methodName,
            1024,
            &nameLength,
            &methodAttr,
            &sigBlob,
            &sigBlobLength,
            &codeRva,
            nullptr);

        if (SUCCEEDED(hr))
        {
            info.methodName = methodName;
            info.attributes = methodAttr;
            info.isStatic = (methodAttr & mdStatic) != 0;
            info.isPublic = (methodAttr & mdPublic) != 0;
            info.isPrivate = (methodAttr & mdPrivate) != 0;
            info.isVirtual = (methodAttr & mdVirtual) != 0;
            info.typeToken = typeDef;

            // Parse method signature for arguments
            if (sigBlob != nullptr)
            {
                ULONG offset = 0;
                ULONG callConv = sigBlob[offset++];
                ULONG paramCount = sigBlob[offset++];
                info.argCount = paramCount;

                // Skip return type
                while (offset < sigBlobLength && sigBlob[offset] != ELEMENT_TYPE_END)
                    offset++;
                offset++;

                // Read parameter types
                for (ULONG i = 0; i < paramCount && offset < sigBlobLength; i++)
                {
                    TypeInfo argType;
                    ULONG elemType = sigBlob[offset++];
                    argType.isValueType = (elemType == ELEMENT_TYPE_VALUETYPE);

                    if (argType.isValueType)
                    {
                        // Read type token
                        argType.typeToken = *(mdToken *)(sigBlob + offset);
                        offset += sizeof(mdToken);

                        // Get type name
                        WCHAR typeName[1024];
                        metaData->GetTypeDefProps(
                            argType.typeToken,
                            typeName,
                            1024,
                            &nameLength,
                            nullptr,
                            nullptr);
                        argType.typeName = typeName;
                    }
                    info.argTypes.push_back(argType);
                }
            }

            // Get class name
            DWORD typeDefFlags;
            hr = metaData->GetTypeDefProps(
                typeDef,
                className,
                1024,
                &nameLength,
                &typeDefFlags,
                nullptr);

            if (SUCCEEDED(hr))
            {
                info.className = className;
                info.isValueType = (typeDefFlags & tdClass) == 0;
            }

            // Get assembly name
            AssemblyID assemblyId;
            profilerInfo->GetModuleInfo(moduleId, nullptr, 0, nullptr, nullptr, &assemblyId);

            WCHAR assemblyName[1024];
            ULONG assemblyNameLength;
            hr = profilerInfo->GetAssemblyInfo(
                assemblyId,
                1024,
                &assemblyNameLength,
                assemblyName,
                nullptr,
                nullptr);

            if (SUCCEEDED(hr))
            {
                info.assemblyName = assemblyName;
            }

            // Get method IL
            LPCBYTE ilHeader = nullptr;
            ULONG ilSize = 0;
            hr = profilerInfo->GetILFunctionBody(
                moduleId,
                token,
                &ilHeader,
                &ilSize);

            if (SUCCEEDED(hr))
            {
                info.codeSize = ilSize;
                info.ilCode = const_cast<BYTE *>(ilHeader);
            }
        }

        metaData->Release();
        return info;
    }
};
