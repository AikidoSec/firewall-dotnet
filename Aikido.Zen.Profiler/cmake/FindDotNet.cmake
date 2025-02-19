# Find .NET SDK
# This module defines:
#  DOTNET_FOUND - Whether .NET SDK was found
#  DOTNET_EXE - Path to dotnet executable
#  DOTNET_VERSION - Version of .NET SDK
#  DOTNET_HOST_PATH - Path to .NET host
#  DOTNET_PACKAGES_DIR - Path to .NET packages directory

# Find dotnet executable
find_program(DOTNET_EXE
    NAMES dotnet
    PATHS
        "$ENV{ProgramFiles}/dotnet"
        "$ENV{ProgramFiles(x86)}/dotnet"
        "$ENV{HOME}/.dotnet"
        "/usr/local/share/dotnet"
        "/usr/share/dotnet"
    DOC "Path to dotnet executable"
)

if(DOTNET_EXE)
    # Get .NET version
    execute_process(
        COMMAND ${DOTNET_EXE} --version
        OUTPUT_VARIABLE DOTNET_VERSION
        OUTPUT_STRIP_TRAILING_WHITESPACE
    )

    # Get .NET host path
    get_filename_component(DOTNET_HOST_PATH ${DOTNET_EXE} DIRECTORY)

    # Get .NET packages directory
    if(WIN32)
        set(DOTNET_PACKAGES_DIR "$ENV{USERPROFILE}/.nuget/packages")
    else()
        set(DOTNET_PACKAGES_DIR "$ENV{HOME}/.nuget/packages")
    endif()

    # Handle REQUIRED and QUIET arguments
    include(FindPackageHandleStandardArgs)
    find_package_handle_standard_args(DotNet
        REQUIRED_VARS DOTNET_EXE DOTNET_HOST_PATH
        VERSION_VAR DOTNET_VERSION
    )

    mark_as_advanced(DOTNET_EXE DOTNET_VERSION DOTNET_HOST_PATH DOTNET_PACKAGES_DIR)
endif()
