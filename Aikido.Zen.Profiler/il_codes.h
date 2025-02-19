#pragma once
#include <cor.h>

// see https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes for more information on IL opcodes
enum IL_OPCODES
{
    CEE_NOP = 0x00,
    CEE_LDARG_0 = 0x02,
    CEE_LDARG_1 = 0x03,
    CEE_LDARG_2 = 0x04,
    CEE_LDARG_3 = 0x05,
    CEE_LDLOC_0 = 0x06,
    CEE_STLOC_0 = 0x0A,
    CEE_LDARG = 0xFE09,
    CEE_LDLOC = 0xFE0C,
    CEE_STLOC = 0xFE0E,
    CEE_CALL = 0x28,
    CEE_RET = 0x2A,
    CEE_BOX = 0x8C,
    CEE_NEWARR = 0x8D,
    CEE_LDSTR = 0x72,
    CEE_LDC_I4 = 0x20,
    CEE_LDC_I4_0 = 0x16,
    CEE_LDC_I4_1 = 0x17,
    CEE_LDC_I4_2 = 0x18,
    CEE_LDC_I4_3 = 0x19,
    CEE_LDC_I4_4 = 0x1A,
    CEE_LDC_I4_5 = 0x1B,
    CEE_LDC_I4_6 = 0x1C,
    CEE_LDC_I4_7 = 0x1D,
    CEE_LDC_I4_8 = 0x1E,
    CEE_STELEM_REF = 0xA2,
    CEE_LOCALS = 0xF0 // Custom opcode for local var signature
};

// Helper class for generating IL instructions
struct ILInstructions
{
    static BYTE *LoadString(const wchar_t *str, size_t &size);
    static BYTE *CallMethod(mdMethodDef methodToken, size_t &size);
    static BYTE *LoadArg(int argIndex, size_t &size);
    static BYTE *StoreLocal(int localIndex, size_t &size);
    static BYTE *Box(mdTypeRef typeRef, size_t &size);
    static BYTE *NewArray(mdTypeRef elementTypeRef, size_t &size);
    static BYTE *LoadConstantI4(int value, size_t &size);
    static BYTE *StoreElementRef(size_t &size);
};
