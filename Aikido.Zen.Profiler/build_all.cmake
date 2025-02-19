# Build configuration for all platforms and architectures
include(ExternalProject)

# Define supported architectures
set(SUPPORTED_ARCHITECTURES "x64" "arm" "arm64")

# Define supported platforms
if(WIN32)
    set(SUPPORTED_PLATFORMS "windows")
else()
    set(SUPPORTED_PLATFORMS "linux" "osx")
endif()

# Set output directories to avoid nested directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib")
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib")

# Apply the same settings for all configurations
foreach(OUTPUTCONFIG ${CMAKE_CONFIGURATION_TYPES})
    string(TOUPPER "${OUTPUTCONFIG}" OUTPUTCONFIG_UPPER)
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_${OUTPUTCONFIG_UPPER} "${CMAKE_BINARY_DIR}/bin")
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_${OUTPUTCONFIG_UPPER} "${CMAKE_BINARY_DIR}/lib")
    set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY_${OUTPUTCONFIG_UPPER} "${CMAKE_BINARY_DIR}/lib")
endforeach()

# Function to generate platform/architecture specific build
function(add_platform_arch_build PLATFORM ARCH TOOLCHAIN_FILE)
    set(BUILD_DIR "${CMAKE_BINARY_DIR}")
    set(INSTALL_DIR "${CMAKE_BINARY_DIR}")

    ExternalProject_Add(
        aikido_${PLATFORM}_${ARCH}
        SOURCE_DIR ${CMAKE_CURRENT_SOURCE_DIR}
        CMAKE_ARGS
            -DCMAKE_INSTALL_PREFIX=${INSTALL_DIR}
            -DCMAKE_TOOLCHAIN_FILE=${TOOLCHAIN_FILE}
            -DCMAKE_BUILD_TYPE=Release
            -DCMAKE_RUNTIME_OUTPUT_DIRECTORY=${CMAKE_BINARY_DIR}/bin
            -DCMAKE_LIBRARY_OUTPUT_DIRECTORY=${CMAKE_BINARY_DIR}/lib
            -DCMAKE_ARCHIVE_OUTPUT_DIRECTORY=${CMAKE_BINARY_DIR}/lib
        BUILD_COMMAND
            ${CMAKE_COMMAND} --build . --config Release
        INSTALL_COMMAND
            ${CMAKE_COMMAND} --install . --config Release
    )
endfunction()

# Generate all platform/architecture combinations
foreach(PLATFORM ${SUPPORTED_PLATFORMS})
    foreach(ARCH ${SUPPORTED_ARCHITECTURES})
        set(TOOLCHAIN_FILE "${CMAKE_CURRENT_LIST_DIR}/cmake/toolchains/toolchain_${PLATFORM}_${ARCH}.cmake")
        if(EXISTS ${TOOLCHAIN_FILE})
            add_platform_arch_build(${PLATFORM} ${ARCH} ${TOOLCHAIN_FILE})
        else()
            message(WARNING "Toolchain file not found: ${TOOLCHAIN_FILE}")
        endif()
    endforeach()
endforeach()

# Add a target to build everything
add_custom_target(build_all ALL)
foreach(PLATFORM ${SUPPORTED_PLATFORMS})
    foreach(ARCH ${SUPPORTED_ARCHITECTURES})
        add_dependencies(build_all aikido_${PLATFORM}_${ARCH})
    endforeach()
endforeach()
