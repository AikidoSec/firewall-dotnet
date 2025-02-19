#include "il_codes.h"
#include <vector>
#include <memory>

BYTE *ILInstructions::LoadString(const wchar_t *str, size_t &size)
{
    static std::vector<BYTE> buffer;
    buffer.clear();
    buffer.push_back(CEE_LDSTR);
    // String token will be replaced by metadata emit
    buffer.push_back(0);
    buffer.push_back(0);
    buffer.push_back(0);
    buffer.push_back(0);
    size = buffer.size();
    return buffer.data();
}

BYTE *ILInstructions::CallMethod(mdMethodDef methodToken, size_t &size)
{
    static std::vector<BYTE> buffer;
    buffer.clear();
    buffer.push_back(CEE_CALL);
    // Method token
    buffer.push_back((BYTE)(methodToken & 0xFF));
    buffer.push_back((BYTE)((methodToken >> 8) & 0xFF));
    buffer.push_back((BYTE)((methodToken >> 16) & 0xFF));
    buffer.push_back((BYTE)((methodToken >> 24) & 0xFF));
    size = buffer.size();
    return buffer.data();
}

BYTE *ILInstructions::LoadArg(int argIndex, size_t &size)
{
    static std::vector<BYTE> buffer;
    buffer.clear();

    // Optimize for common cases
    if (argIndex <= 3)
    {
        buffer.push_back((BYTE)(CEE_LDARG_0 + argIndex));
    }
    else
    {
        buffer.push_back((BYTE)(CEE_LDARG & 0xFF));
        buffer.push_back((BYTE)((CEE_LDARG >> 8) & 0xFF));
        buffer.push_back((BYTE)(argIndex & 0xFF));
        buffer.push_back((BYTE)((argIndex >> 8) & 0xFF));
    }

    size = buffer.size();
    return buffer.data();
}

BYTE *ILInstructions::StoreLocal(int localIndex, size_t &size)
{
    static std::vector<BYTE> buffer;
    buffer.clear();

    // Optimize for common cases
    if (localIndex == 0)
    {
        buffer.push_back(CEE_STLOC_0);
    }
    else
    {
        buffer.push_back((BYTE)(CEE_STLOC & 0xFF));
        buffer.push_back((BYTE)((CEE_STLOC >> 8) & 0xFF));
        buffer.push_back((BYTE)(localIndex & 0xFF));
        buffer.push_back((BYTE)((localIndex >> 8) & 0xFF));
    }

    size = buffer.size();
    return buffer.data();
}

BYTE *ILInstructions::Box(mdTypeRef typeRef, size_t &size)
{
    static std::vector<BYTE> buffer;
    buffer.clear();
    buffer.push_back(CEE_BOX);
    // Type token
    buffer.push_back((BYTE)(typeRef & 0xFF));
    buffer.push_back((BYTE)((typeRef >> 8) & 0xFF));
    buffer.push_back((BYTE)((typeRef >> 16) & 0xFF));
    buffer.push_back((BYTE)((typeRef >> 24) & 0xFF));
    size = buffer.size();
    return buffer.data();
}

BYTE *ILInstructions::NewArray(mdTypeRef elementTypeRef, size_t &size)
{
    static std::vector<BYTE> buffer;
    buffer.clear();
    buffer.push_back(CEE_NEWARR);
    // Element type token
    buffer.push_back((BYTE)(elementTypeRef & 0xFF));
    buffer.push_back((BYTE)((elementTypeRef >> 8) & 0xFF));
    buffer.push_back((BYTE)((elementTypeRef >> 16) & 0xFF));
    buffer.push_back((BYTE)((elementTypeRef >> 24) & 0xFF));
    size = buffer.size();
    return buffer.data();
}

BYTE *ILInstructions::LoadConstantI4(int value, size_t &size)
{
    static std::vector<BYTE> buffer;
    buffer.clear();

    // Optimize for common cases
    if (value >= 0 && value <= 8)
    {
        buffer.push_back((BYTE)(CEE_LDC_I4_0 + value));
    }
    else
    {
        buffer.push_back(CEE_LDC_I4);
        buffer.push_back((BYTE)(value & 0xFF));
        buffer.push_back((BYTE)((value >> 8) & 0xFF));
        buffer.push_back((BYTE)((value >> 16) & 0xFF));
        buffer.push_back((BYTE)((value >> 24) & 0xFF));
    }

    size = buffer.size();
    return buffer.data();
}

BYTE *ILInstructions::StoreElementRef(size_t &size)
{
    static std::vector<BYTE> buffer;
    buffer.clear();
    buffer.push_back(CEE_STELEM_REF);
    size = buffer.size();
    return buffer.data();
}
