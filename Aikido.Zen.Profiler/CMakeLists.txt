# CMakeList.txt : CMake project for Aikido.Zen.Profiler, include source and define
# project specific logic here.
#
cmake_minimum_required(VERSION 3.10)

# Enable Hot Reload for MSVC compilers if supported.
if (POLICY CMP0141)
  cmake_policy(SET CMP0141 NEW)
  set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
endif()

project("Aikido.Zen.Profiler" CXX)

# Set C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Enable position independent code
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Add cmake modules path
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")

# Architecture detection
if(CMAKE_SYSTEM_PROCESSOR MATCHES "^(x86_64|amd64|AMD64)$")
    set(ARCH_X64 TRUE)
    set(ARCH_NAME "x64")
elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "^(i[3-6]86|x86)$")
    set(ARCH_X86 TRUE)
    set(ARCH_NAME "x86")
elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "^(aarch64|arm64)$")
    set(ARCH_ARM64 TRUE)
    set(ARCH_NAME "arm64")
elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "^(arm|armv7)$")
    set(ARCH_ARM TRUE)
    set(ARCH_NAME "arm")
else()
    message(FATAL_ERROR "Unsupported architecture: ${CMAKE_SYSTEM_PROCESSOR}")
endif()

# Architecture-specific compile definitions
if(ARCH_X64)
    add_compile_definitions(TARGET_AMD64=1 HOST_AMD64=1 BIT64=1)
elseif(ARCH_X86)
    add_compile_definitions(TARGET_X86=1 HOST_X86=1)
elseif(ARCH_ARM64)
    add_compile_definitions(TARGET_ARM64=1 HOST_ARM64=1 BIT64=1)
elseif(ARCH_ARM)
    add_compile_definitions(TARGET_ARM=1 HOST_ARM=1)
endif()

# Find .NET SDK
find_program(DOTNET_EXE
    NAMES dotnet
    PATHS
        "${CMAKE_PROGRAM_FILES_X86}/dotnet"
        "${CMAKE_PROGRAM_FILES}/dotnet"
        "$ENV{HOME}/.dotnet"
        "/usr/local/share/dotnet"
        "/usr/share/dotnet"
    DOC "Path to dotnet executable"
)

# Add source files
set(SOURCES
    profiler_exports.cpp
    profiler_exports.h
    aikido_profiler.h
    il_codes.h
    il_rewriter.h
    instrumentation_filter.h
    metadata_helper.h
    method_info.h
    platform_intrinsics.h
    platform_ops.h
)

# Create shared library
add_library(AikidoProfiler SHARED ${SOURCES})

# Set output name and properties based on platform and architecture
if(WIN32)
    set(PLATFORM_NAME "windows")
    set_target_properties(AikidoProfiler PROPERTIES
        OUTPUT_NAME "Aikido.Zen.Profiler.${PLATFORM_NAME}.${ARCH_NAME}"
        PREFIX ""
        SUFFIX ".dll"
    )
elseif(APPLE)
    set(PLATFORM_NAME "osx")
    set_target_properties(AikidoProfiler PROPERTIES
        OUTPUT_NAME "Aikido.Zen.Profiler.${PLATFORM_NAME}.${ARCH_NAME}"
        PREFIX "lib"
        SUFFIX ".dylib"
    )
else()
    set(PLATFORM_NAME "linux")
    set_target_properties(AikidoProfiler PROPERTIES
        OUTPUT_NAME "Aikido.Zen.Profiler.${PLATFORM_NAME}.${ARCH_NAME}"
        PREFIX "lib"
        SUFFIX ".so"
    )
endif()

# Set the linker language explicitly
if(NOT DEFINED SCRIPT_DIR)
    set(SCRIPT_DIR ${CMAKE_CURRENT_SOURCE_DIR})
endif()

set_target_properties(AikidoProfiler PROPERTIES
    LINKER_LANGUAGE CXX
    RUNTIME_OUTPUT_DIRECTORY "${SCRIPT_DIR}/../Aikido.Zen.Core/libraries"
    LIBRARY_OUTPUT_DIRECTORY "${SCRIPT_DIR}/../Aikido.Zen.Core/libraries"
    ARCHIVE_OUTPUT_DIRECTORY "${SCRIPT_DIR}/../Aikido.Zen.Core/libraries"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${SCRIPT_DIR}/../Aikido.Zen.Core/libraries"
    LIBRARY_OUTPUT_DIRECTORY_DEBUG "${SCRIPT_DIR}/../Aikido.Zen.Core/libraries"
    ARCHIVE_OUTPUT_DIRECTORY_DEBUG "${SCRIPT_DIR}/../Aikido.Zen.Core/libraries"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${SCRIPT_DIR}/../Aikido.Zen.Core/libraries"
    LIBRARY_OUTPUT_DIRECTORY_RELEASE "${SCRIPT_DIR}/../Aikido.Zen.Core/libraries"
    ARCHIVE_OUTPUT_DIRECTORY_RELEASE "${SCRIPT_DIR}/../Aikido.Zen.Core/libraries"
)

# Add include directories
message(STATUS "CORECLR_PATH: ${CORECLR_PATH}")
if(DEFINED CORECLR_PATH)
    # CORECLR_PATH is already set from command line
elseif(EXISTS "$ENV{HOME}/coreclr")
    set(CORECLR_PATH "$ENV{HOME}/coreclr")
else()
    message(FATAL_ERROR "CORECLR_PATH must be set to the CoreCLR source directory or ~/coreclr must exist")
endif()

# Common include directories
target_include_directories(AikidoProfiler PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CORECLR_PATH}/src/coreclr/inc                # Core CLR headers
    ${CORECLR_PATH}/src/coreclr/pal/inc           # Platform abstraction layer
    ${CORECLR_PATH}/src/coreclr/pal/prebuilt/inc  # PAL definitions
    ${CORECLR_PATH}/src/coreclr/inc/${ARCH_NAME}  # Architecture-specific headers
)

# Platform-specific settings
if(WIN32)
    target_compile_definitions(AikidoProfiler PRIVATE
        WIN32
        _WINDOWS
        _USRDLL
        NOMINMAX
        UNICODE
        _UNICODE
        _WIN32_WINNT=0x0A00  # Target Windows 10
        WINVER=0x0A00        # Target Windows 10
        PLATFORM_WINDOWS=1
    )

    # Add Windows SDK includes
    if(MSVC)
        target_include_directories(AikidoProfiler PRIVATE
            "$ENV{WindowsSdkDir}Include/$ENV{WindowsSDKVersion}/um"      # Windows API
            "$ENV{WindowsSdkDir}Include/$ENV{WindowsSDKVersion}/shared"  # Common Windows definitions
            "$ENV{WindowsSdkDir}Include/$ENV{WindowsSDKVersion}/ucrt"    # C Runtime
        )

        # Architecture-specific compiler flags for MSVC
        if(ARCH_X64)
            set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /D_AMD64_")
        elseif(ARCH_X86)
            set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /D_X86_")
        elseif(ARCH_ARM64)
            set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /D_ARM64_")
        elseif(ARCH_ARM)
            set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /D_ARM_")
        endif()
    endif()
else()
    target_compile_definitions(AikidoProfiler PRIVATE
        PLATFORM_UNIX=1
        PAL_IMPLEMENTATION
        _GNU_SOURCE
    )

    # Add Unix-specific includes
    target_include_directories(AikidoProfiler PRIVATE
        ${CORECLR_PATH}/src/coreclr/pal/src/include  # PAL Unix implementation
        ${CORECLR_PATH}/src/coreclr/pal/src         # PAL sources
    )

    # Architecture-specific compiler flags for GCC/Clang
    if(ARCH_X64)
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -m64")
    elseif(ARCH_X86)
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -m32")
    elseif(ARCH_ARM OR ARCH_ARM64)
        # ARM-specific optimizations
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fPIC")
    endif()

    # Link against required Unix libraries
    target_link_libraries(AikidoProfiler PRIVATE
        dl      # Dynamic linking support
        pthread # Threading support
    )
endif()

# Add managed project
add_custom_target(AikidoProfilerManaged
    COMMAND ${DOTNET_EXE} build ${CMAKE_CURRENT_SOURCE_DIR}/Aikido.Zen.Profiler.csproj
    WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
)

# Make managed build depend on native build
add_dependencies(AikidoProfilerManaged AikidoProfiler)

# Install targets with architecture-specific paths
install(TARGETS AikidoProfiler
    RUNTIME DESTINATION "bin/${ARCH_NAME}"
    LIBRARY DESTINATION "lib/${ARCH_NAME}"
)

# Install managed assemblies
install(FILES
    ${CMAKE_CURRENT_BINARY_DIR}/bin/${ARCH_NAME}/Debug/netstandard2.0/Aikido.Zen.Profiler.dll
    DESTINATION "lib/${ARCH_NAME}"
)

# Ensure build_all target is added
add_custom_target(build_all ALL)
foreach(PLATFORM ${SUPPORTED_PLATFORMS})
    foreach(ARCH ${SUPPORTED_ARCHITECTURES})
        add_dependencies(build_all aikido_${PLATFORM}_${ARCH})
    endforeach()
endforeach()
