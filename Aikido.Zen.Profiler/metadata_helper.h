#pragma once
#include <cor.h>
#include <corprof.h>
#include "method_info.h"

class MetadataHelper
{
public:
    static HRESULT GetMethodSignature(IMetaDataImport *metaData,
                                      mdMethodDef methodDef,
                                      std::wstring &signature)
    {
        PCCOR_SIGNATURE sigBlob;
        ULONG sigSize;
        HRESULT hr = metaData->GetMethodProps(
            methodDef,
            nullptr,  // class token
            nullptr,  // method name
            0,        // method name length
            nullptr,  // name length required
            nullptr,  // attributes
            &sigBlob, // signature blob
            &sigSize, // signature size
            nullptr,  // RVA
            nullptr   // implementation flags
        );

        if (FAILED(hr))
            return hr;

        // Convert signature blob to string representation
        // [Implementation details...]

        return S_OK;
    }
};
