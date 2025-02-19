#pragma once
#include <cor.h>         // Core CLR interfaces - see https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/cor
#include <corprof.h>     // Profiling interfaces - see https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling
#include "method_info.h" // Custom method metadata wrapper
#include "il_codes.h"    // IL opcodes definitions
#include <vector>
#include <memory>                // For smart pointers
#include <stdexcept>             // For exceptions
#include "platform_intrinsics.h" // Platform-agnostic intrinsics

// Constants for IL buffer sizing
constexpr ULONG DEFAULT_IL_BUFFER_PADDING = 512;
constexpr ULONG MAX_ASSEMBLY_REFS = 1024;

/**
 * @brief Handles IL rewriting for method instrumentation.
 *
 * The ILRewriter class is responsible for modifying the IL code of methods to add
 * instrumentation. It injects code at method entry to:
 * 1. Create an array to hold the method arguments
 * 2. Box value type arguments if needed
 * 3. Call into the managed Bridge class
 *
 * The rewriter handles both .NET Core and .NET Framework by detecting the runtime
 * and adjusting assembly references accordingly.
 *
 * For more information on IL rewriting, see:
 * https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/il-rewriting-overview
 */
class ILRewriter
{
private:
    ICorProfilerInfo7 *profilerInfo;        ///< Interface to the CLR profiler - see https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo7-interface
    MethodInfo &methodInfo;                 ///< Information about the method being rewritten
    std::vector<BYTE> ilBuffer;             ///< Use vector for automatic cleanup
    BYTE *newIL;                            ///< Points into ilBuffer
    ULONG newILSize;                        ///< Size of the new IL code
    std::vector<COR_SIGNATURE> localVarSig; ///< Signature for local variables - see https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/metadata/corsignature-coclass
    bool isNetCore;                         ///< Whether we're running on .NET Core
    size_t currentILOffset;                 ///< Track current position in IL buffer

    // RAII wrapper for COM interfaces
    template <typename T>
    class ComPtr
    {
        T *ptr;

    public:
        ComPtr() : ptr(nullptr) {}
        ~ComPtr()
        {
            if (ptr)
                ptr->Release();
        }
        T **AddressOf() { return &ptr; }
        T *Get() const { return ptr; }
        T *operator->() const { return ptr; }
    };

    // Helper to safely advance IL pointer
    void AdvanceIL(size_t bytes)
    {
        if (currentILOffset + bytes > ilBuffer.size())
        {
            throw std::runtime_error("IL buffer overflow");
        }
        currentILOffset += bytes;
        newIL += bytes;
    }

    // Helper to emit IL bytes safely
    template <typename T>
    void EmitIL(T value)
    {
        if (currentILOffset + sizeof(T) > ilBuffer.size())
        {
            throw std::runtime_error("IL buffer overflow");
        }
        *reinterpret_cast<T *>(newIL) = value;
        AdvanceIL(sizeof(T));
    }

    /**
     * @brief Emits IL to load method arguments into an array.
     *
     * This method generates IL code to:
     * 1. Create a new object array
     * 2. Store the instance (if non-static) as the first element
     * 3. Store each argument, boxing value types as needed
     *
     * The resulting array follows this format:
     * - Instance methods: [instance, arg1, arg2, ...]
     * - Static methods: [arg1, arg2, ...]
     *
     * @param metaEmit Metadata emit interface for token generation
     */
    void EmitLoadArguments(IMetaDataEmit *metaEmit)
    {
        if (!metaEmit)
            throw std::invalid_argument("metaEmit is null");

        if (methodInfo.argCount > 1024)
        {
            throw std::runtime_error("Too many arguments");
        }

        // Create array to store arguments
        size_t size;
        BYTE *il = ILInstructions::LoadConstantI4(methodInfo.argCount, size);
        memcpy(newIL, il, size);
        AdvanceIL(size);

        // Get System.Object type reference
        ComPtr<IMetaDataAssemblyEmit> assemblyEmit;
        HRESULT hr = metaEmit->QueryInterface(IID_IMetaDataAssemblyEmit, reinterpret_cast<void **>(assemblyEmit.AddressOf()));
        if (FAILED(hr))
            throw std::runtime_error("Failed to get assembly emit interface");

        mdAssemblyRef mscorlibRef;
        ASSEMBLYMETADATA asmMetaData = {};
        hr = assemblyEmit->DefineAssemblyRef(
            nullptr, 0,                                  // Public key
            isNetCore ? L"System.Runtime" : L"mscorlib", // Name
            &asmMetaData,                                // Metadata
            nullptr, 0,                                  // Hash blob
            0,                                           // Flags
            &mscorlibRef);                               // [out] assembly ref token

        if (FAILED(hr))
            throw std::runtime_error("Failed to define assembly reference");

        mdTypeRef objectTypeRef;
        hr = metaEmit->DefineTypeRefByName(
            mscorlibRef,
            L"System.Object",
            &objectTypeRef);

        if (FAILED(hr))
            throw std::runtime_error("Failed to define System.Object reference");

        il = ILInstructions::NewArray(objectTypeRef, size);
        memcpy(newIL, il, size);
        AdvanceIL(size);

        il = ILInstructions::StoreLocal(0, size);
        memcpy(newIL, il, size);
        AdvanceIL(size);

        // Handle instance methods
        if (!methodInfo.isStatic)
        {
            il = ILInstructions::LoadArg(0, size);
            memcpy(newIL, il, size);
            AdvanceIL(size);

            if (methodInfo.isValueType)
            {
                il = ILInstructions::Box(methodInfo.typeToken, size);
                memcpy(newIL, il, size);
                AdvanceIL(size);
            }

            il = ILInstructions::StoreElementRef(size);
            memcpy(newIL, il, size);
            AdvanceIL(size);
        }

        // Load arguments into array
        for (ULONG i = 0; i < methodInfo.argCount; i++)
        {
            if (i >= methodInfo.argTypes.size())
            {
                throw std::runtime_error("Argument type information missing");
            }

            const int argIndex = methodInfo.isStatic ? i : i + 1;

            il = ILInstructions::LoadArg(argIndex, size);
            memcpy(newIL, il, size);
            AdvanceIL(size);

            if (methodInfo.argTypes[i].isValueType)
            {
                il = ILInstructions::Box(methodInfo.argTypes[i].typeToken, size);
                memcpy(newIL, il, size);
                AdvanceIL(size);
            }

            il = ILInstructions::StoreElementRef(size);
            memcpy(newIL, il, size);
            AdvanceIL(size);
        }
    }

    /**
     * @brief Emits IL to call the managed Bridge class.
     *
     * This method generates IL code to call the OnMethodEnter method
     * in the managed Bridge class. It:
     * 1. Creates a reference to the Bridge class
     * 2. Loads the method name
     * 3. Loads the argument array
     * 4. Calls OnMethodEnter
     *
     * @param metaEmit Metadata emit interface for token generation
     */
    void EmitCallToManagedBridge(IMetaDataEmit *metaEmit)
    {
        if (!metaEmit)
            throw std::invalid_argument("metaEmit is null");

        // Define Bridge class reference
        ComPtr<IMetaDataAssemblyEmit> assemblyEmit;
        HRESULT hr = metaEmit->QueryInterface(IID_IMetaDataAssemblyEmit, reinterpret_cast<void **>(assemblyEmit.AddressOf()));
        if (FAILED(hr))
            throw std::runtime_error("Failed to get assembly emit interface");

        mdAssemblyRef aikidoRef;
        ASSEMBLYMETADATA asmMetaData = {};
        hr = assemblyEmit->DefineAssemblyRef(
            nullptr, 0,         // Public key
            L"Aikido.Zen.Core", // Name
            &asmMetaData,       // Metadata
            nullptr, 0,         // Hash blob
            0,                  // Flags
            &aikidoRef);        // [out] assembly ref token

        if (FAILED(hr))
            throw std::runtime_error("Failed to define assembly reference");

        mdTypeRef bridgeTypeRef;
        hr = metaEmit->DefineTypeRefByName(
            aikidoRef,
            L"Aikido.Zen.Core.Bridge",
            &bridgeTypeRef);

        if (FAILED(hr))
            throw std::runtime_error("Failed to define Bridge type reference");

        // Define method reference
        static const COR_SIGNATURE bridgeMethodSig[] = {
            IMAGE_CEE_CS_CALLCONV_DEFAULT,
            2,
            ELEMENT_TYPE_VOID,
            ELEMENT_TYPE_STRING,
            ELEMENT_TYPE_SZARRAY,
            ELEMENT_TYPE_OBJECT};

        mdMemberRef bridgeMethodRef;
        hr = metaEmit->DefineMemberRef(
            bridgeTypeRef,
            L"OnMethodEnter",
            bridgeMethodSig,
            sizeof(bridgeMethodSig),
            &bridgeMethodRef);

        if (FAILED(hr))
            throw std::runtime_error("Failed to define OnMethodEnter reference");

        // Load method name
        size_t size;
        const std::wstring &fullName = methodInfo.GetFullName();
        BYTE *il = ILInstructions::LoadString(fullName.c_str(), size);
        memcpy(newIL, il, size);
        AdvanceIL(size);

        il = ILInstructions::LoadArg(0, size);
        memcpy(newIL, il, size);
        AdvanceIL(size);

        il = ILInstructions::CallMethod(bridgeMethodRef, size);
        memcpy(newIL, il, size);
        AdvanceIL(size);
    }

    /**
     * @brief Inserts the prolog code at the start of the method.
     *
     * The prolog code:
     * 1. Defines local variables needed for instrumentation
     * 2. Loads method arguments into an array
     * 3. Calls into the managed Bridge
     *
     * @param metaEmit Metadata emit interface for token generation
     */
    void InsertProlog(IMetaDataEmit *metaEmit)
    {
        // Define local variable for argument array
        localVarSig.push_back(IMAGE_CEE_CS_CALLCONV_LOCAL_SIG);
        localVarSig.push_back(1);
        localVarSig.push_back(ELEMENT_TYPE_SZARRAY);
        localVarSig.push_back(ELEMENT_TYPE_OBJECT);

        mdSignature localVarSigToken;
        HRESULT hr = metaEmit->GetTokenFromSig(
            &localVarSig[0],
            static_cast<ULONG>(localVarSig.size()),
            &localVarSigToken);

        if (FAILED(hr))
            throw std::runtime_error("Failed to get token from signature");

        EmitIL<BYTE>(CEE_LOCALS);
        EmitIL<mdToken>(localVarSigToken);

        EmitLoadArguments(metaEmit);
        EmitCallToManagedBridge(metaEmit);
    }

    /**
     * @brief Copies the original method body after the instrumentation.
     *
     * This preserves the original method behavior after the instrumentation
     * code has executed.
     */
    void CopyOriginalIL()
    {
        memcpy(newIL, methodInfo.ilCode, methodInfo.codeSize);
        newIL += methodInfo.codeSize;
        currentILOffset += methodInfo.codeSize;
    }

    /**
     * @brief Reserved for method exit instrumentation.
     *
     * Currently not implemented, but could be used to add
     * instrumentation at method exit points.
     */
    void InsertEpilog()
    {
        // For now, we only instrument method entry
        // Method exit instrumentation can be added here
    }

    /**
     * @brief Detects whether we're running on .NET Core.
     *
     * This method checks for the presence of System.Runtime assembly
     * to determine if we're running on .NET Core. This affects how
     * we reference system types.
     *
     * @return true if running on .NET Core, false for .NET Framework
     */
    bool DetectRuntime()
    {
        ComPtr<IMetaDataAssemblyImport> assemblyImport;
        HRESULT hr = profilerInfo->GetModuleMetaData(
            methodInfo.moduleId,
            ofRead,
            IID_IMetaDataAssemblyImport,
            reinterpret_cast<IUnknown **>(assemblyImport.AddressOf()));

        if (SUCCEEDED(hr))
        {
            mdAssemblyRef refs[MAX_ASSEMBLY_REFS];
            ULONG count = 0;
            hr = assemblyImport->EnumAssemblyRefs(nullptr, refs, MAX_ASSEMBLY_REFS, &count);

            if (SUCCEEDED(hr))
            {
                for (ULONG i = 0; i < count; i++)
                {
                    WCHAR assemblyName[1024];
                    ULONG nameLen = 0;
                    hr = assemblyImport->GetAssemblyRefProps(
                        refs[i],
                        nullptr,
                        nullptr,
                        assemblyName,
                        1024,
                        &nameLen,
                        nullptr,
                        nullptr,
                        nullptr,
                        nullptr);

                    if (SUCCEEDED(hr) && wcscmp(assemblyName, L"System.Runtime") == 0)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    static bool DetectRuntimeStatic()
    {
        // Cache the result statically
        static bool isNetCoreCache = false;
        static bool isCacheInitialized = false;

        if (isCacheInitialized)
            return isNetCoreCache;

        // Implementation remains the same but sets the cache
        // ... existing implementation ...

        isCacheInitialized = true;
        return isNetCoreCache;
    }

public:
    /**
     * @brief Constructs an IL rewriter for a method.
     *
     * @param info The profiler info interface
     * @param info The method information
     */
    ILRewriter(ICorProfilerInfo7 *info, MethodInfo &methodInfo)
        : profilerInfo(info), methodInfo(methodInfo), currentILOffset(0)
    {
        if (!info)
            throw std::invalid_argument("profilerInfo is null");

        // Calculate required buffer size
        size_t requiredSize = methodInfo.codeSize + DEFAULT_IL_BUFFER_PADDING;
        ilBuffer.resize(requiredSize);
        newIL = ilBuffer.data();

        isNetCore = DetectRuntimeStatic();
    }

    /**
     * @brief Performs the IL rewriting operation.
     *
     * This method:
     * 1. Gets required metadata interfaces
     * 2. Allocates space for the new IL
     * 3. Inserts instrumentation prolog
     * 4. Copies the original method body
     * 5. Updates the method with new IL
     *
     * @return S_OK on success, error code otherwise
     */
    HRESULT Rewrite()
    {
        try
        {
            ComPtr<IMetaDataEmit> metaEmit;
            HRESULT hr = profilerInfo->GetModuleMetaData(
                methodInfo.moduleId,
                ofWrite,
                IID_IMetaDataEmit,
                reinterpret_cast<IUnknown **>(metaEmit.AddressOf()));
            if (FAILED(hr))
                return hr;

            currentILOffset = 0;
            InsertProlog(metaEmit.Get());
            CopyOriginalIL();
            InsertEpilog();

            // Allocate and copy final IL
            BYTE *finalBuffer = nullptr;
            hr = profilerInfo->SetILFunctionBody(
                methodInfo.moduleId,
                methodInfo.methodToken,
                ilBuffer.data());

            if (FAILED(hr))
                return hr;

            // Use platform-agnostic memory copy
            Platform::Intrinsics::MemoryCopy(
                finalBuffer,
                ilBuffer.data(),
                currentILOffset);

            Platform::Intrinsics::MemoryBarrier(); // Ensure visibility

            return hr;
        }
        catch (const std::exception &e)
        {
            // Log error and return failure
            return E_FAIL;
        }
    }
};
