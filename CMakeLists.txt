cmake_minimum_required(VERSION 3.25)

project(VeloraEngine
    VERSION 0.2.0
    DESCRIPTION "Cross-platform visual novel runtime"
    LANGUAGES C CXX
)

include(FetchContent)
include(GNUInstallDirs)

option(VELORA_BUILD_TESTS "Build the engine test suite" ON)
option(VELORA_WARNINGS_AS_ERRORS "Treat Velora warnings as errors" OFF)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(BUILD_SHARED_LIBS OFF CACHE BOOL "Build static third-party libraries" FORCE)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE RelWithDebInfo CACHE STRING "Build type" FORCE)
endif()

function(velora_fetch name repository revision)
    FetchContent_Declare(${name}
        GIT_REPOSITORY "${repository}"
        GIT_TAG "${revision}"
        GIT_SHALLOW FALSE
        GIT_PROGRESS TRUE
    )
endfunction()

# Revisions are pinned so a clean checkout produces the same build everywhere.
velora_fetch(SDL3 https://github.com/libsdl-org/SDL.git
    b53f1b06447cfe699e2649afc52a1a54e5f19f71)
set(SDL_SHARED OFF CACHE BOOL "" FORCE)
set(SDL_STATIC ON CACHE BOOL "" FORCE)
set(SDL_INSTALL OFF CACHE BOOL "" FORCE)
set(SDL_TEST_LIBRARY OFF CACHE BOOL "" FORCE)
set(SDL_TESTS OFF CACHE BOOL "" FORCE)
set(SDL_UNINSTALL OFF CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(SDL3)
# SDL's aliases are directory-scoped in some CMake versions. Its companion
# projects may therefore call find_package() even though SDL is vendored in the
# same build. The tiny build-tree package below exposes the existing targets.
set(SDL3_DIR "${CMAKE_CURRENT_SOURCE_DIR}/cmake/sdl3" CACHE PATH "" FORCE)

velora_fetch(SDL3_image https://github.com/libsdl-org/SDL_image.git
    0e2eaa923ddea285dfa35c4bf0c0092d3799e2ee)
set(SDLIMAGE_INSTALL OFF CACHE BOOL "" FORCE)
set(SDLIMAGE_VENDORED OFF CACHE BOOL "" FORCE)
set(SDLIMAGE_AVIF OFF CACHE BOOL "" FORCE)
set(SDLIMAGE_AVIF_SAVE OFF CACHE BOOL "" FORCE)
set(SDLIMAGE_JXL OFF CACHE BOOL "" FORCE)
set(SDLIMAGE_WEBP OFF CACHE BOOL "" FORCE)
set(SDLIMAGE_TIF OFF CACHE BOOL "" FORCE)
set(SDLIMAGE_PNG_LIBPNG OFF CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(SDL3_image)

velora_fetch(SDL3_ttf https://github.com/libsdl-org/SDL_ttf.git
    20033f98c3993d6b9170add497418fa96f9f59ac)
set(SDLTTF_INSTALL OFF CACHE BOOL "" FORCE)
set(SDLTTF_VENDORED ON CACHE BOOL "" FORCE)
set(SDLTTF_HARFBUZZ OFF CACHE BOOL "" FORCE)
set(SDLTTF_PLUTOSVG OFF CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(SDL3_ttf)

FetchContent_Declare(miniaudio
    GIT_REPOSITORY https://github.com/mackron/miniaudio.git
    GIT_TAG 9634bedb5b5a2ca38c1ee7108a9358a4e233f14d
    GIT_SHALLOW FALSE
    GIT_PROGRESS TRUE
    SOURCE_SUBDIR velora-header-only
)
FetchContent_MakeAvailable(miniaudio)
add_library(velora_miniaudio INTERFACE)
target_include_directories(velora_miniaudio SYSTEM INTERFACE "${miniaudio_SOURCE_DIR}")

velora_fetch(json https://github.com/nlohmann/json.git
    9cca280a4d0ccf0c08f47a99aa71d1b0e52f8d03)
set(JSON_BuildTests OFF CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(json)

set(VELORA_SOURCES
    src/main.cpp
    src/core/Engine.cpp
    src/core/GameStateManager.cpp
    src/core/Paths.cpp
    src/core/SettingsManager.cpp
    src/core/SystemInfo.cpp
    src/core/TimeManager.cpp
    src/script/Lexer.cpp
    src/script/Parser.cpp
    src/script/ScriptVM.cpp
    src/render/ParticleSystem.cpp
    src/render/PostEffects.cpp
    src/render/Renderer.cpp
    src/render/TweenManager.cpp
    src/audio/AudioMixer.cpp
    src/text/RichTextParser.cpp
    src/asset/AssetManager.cpp
    src/i18n/LocalizationManager.cpp
)

if(WIN32)
    list(APPEND VELORA_SOURCES src/velora.rc)
endif()

add_executable(VeloraEngine ${VELORA_SOURCES})
target_include_directories(VeloraEngine PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/src")
target_compile_definitions(VeloraEngine PRIVATE
    VELORA_VERSION="${PROJECT_VERSION}"
)
target_link_libraries(VeloraEngine PRIVATE
    SDL3::SDL3-static
    SDL3_image::SDL3_image-static
    SDL3_ttf::SDL3_ttf-static
    velora_miniaudio
    nlohmann_json::nlohmann_json
    ${CMAKE_DL_LIBS}
)

if(MSVC)
    target_compile_options(VeloraEngine PRIVATE /W4 /permissive- /Zc:__cplusplus /utf-8)
    if(VELORA_WARNINGS_AS_ERRORS)
        target_compile_options(VeloraEngine PRIVATE /WX)
    endif()
else()
    target_compile_options(VeloraEngine PRIVATE -Wall -Wextra -Wpedantic)
    if(VELORA_WARNINGS_AS_ERRORS)
        target_compile_options(VeloraEngine PRIVATE -Werror)
    endif()
endif()

if(WIN32)
    set_target_properties(VeloraEngine PROPERTIES WIN32_EXECUTABLE "$<NOT:$<CONFIG:Debug>>")
    target_link_libraries(VeloraEngine PRIVATE winmm imm32 version setupapi)
elseif(APPLE)
    set_target_properties(VeloraEngine PROPERTIES
        MACOSX_BUNDLE TRUE
        MACOSX_BUNDLE_BUNDLE_NAME "Velora Engine"
        MACOSX_BUNDLE_GUI_IDENTIFIER "dev.velora.engine"
        MACOSX_BUNDLE_SHORT_VERSION_STRING "${PROJECT_VERSION}"
        MACOSX_BUNDLE_BUNDLE_VERSION "${PROJECT_VERSION}"
    )
endif()

if(APPLE)
    add_custom_command(TARGET VeloraEngine POST_BUILD
        COMMAND "${CMAKE_COMMAND}" -E remove_directory
            "$<TARGET_BUNDLE_CONTENT_DIR:VeloraEngine>/Resources/assets"
        COMMAND "${CMAKE_COMMAND}" -E copy_directory
            "${CMAKE_CURRENT_SOURCE_DIR}/assets"
            "$<TARGET_BUNDLE_CONTENT_DIR:VeloraEngine>/Resources/assets"
        VERBATIM
    )
else()
    add_custom_command(TARGET VeloraEngine POST_BUILD
        COMMAND "${CMAKE_COMMAND}" -E remove_directory
            "$<TARGET_FILE_DIR:VeloraEngine>/assets"
        COMMAND "${CMAKE_COMMAND}" -E copy_directory
            "${CMAKE_CURRENT_SOURCE_DIR}/assets"
            "$<TARGET_FILE_DIR:VeloraEngine>/assets"
        VERBATIM
    )
endif()

install(TARGETS VeloraEngine
    BUNDLE DESTINATION .
    RUNTIME DESTINATION "${CMAKE_INSTALL_BINDIR}"
)
install(FILES LICENSE THIRD_PARTY_NOTICES.md DESTINATION .)
install(FILES "${sdl3_SOURCE_DIR}/LICENSE.txt" DESTINATION licenses RENAME SDL3.txt)
install(FILES "${sdl3_image_SOURCE_DIR}/LICENSE.txt" DESTINATION licenses RENAME SDL3_image.txt)
install(FILES "${sdl3_ttf_SOURCE_DIR}/LICENSE.txt" DESTINATION licenses RENAME SDL3_ttf.txt)
install(FILES "${miniaudio_SOURCE_DIR}/LICENSE" DESTINATION licenses RENAME miniaudio.txt)
install(FILES "${json_SOURCE_DIR}/LICENSE.MIT" DESTINATION licenses RENAME nlohmann_json.txt)
if(NOT APPLE)
    install(DIRECTORY assets/ DESTINATION "${CMAKE_INSTALL_BINDIR}/assets")
endif()

if(VELORA_BUILD_TESTS)
    enable_testing()
    add_executable(VeloraCoreTests
        tests/CoreTests.cpp
        src/core/GameStateManager.cpp
        src/script/Lexer.cpp
        src/script/Parser.cpp
    )
    target_include_directories(VeloraCoreTests PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/src")
    target_compile_definitions(VeloraCoreTests PRIVATE VELORA_SOURCE_DIR="${CMAKE_CURRENT_SOURCE_DIR}")
    target_link_libraries(VeloraCoreTests PRIVATE nlohmann_json::nlohmann_json)
    if(MSVC)
        target_compile_options(VeloraCoreTests PRIVATE /W4 /permissive- /utf-8)
        if(VELORA_WARNINGS_AS_ERRORS)
            target_compile_options(VeloraCoreTests PRIVATE /WX)
        endif()
    else()
        target_compile_options(VeloraCoreTests PRIVATE -Wall -Wextra -Wpedantic)
        if(VELORA_WARNINGS_AS_ERRORS)
            target_compile_options(VeloraCoreTests PRIVATE -Werror)
        endif()
    endif()
    add_test(NAME VeloraCoreTests COMMAND VeloraCoreTests)
    add_test(NAME VeloraVersion COMMAND VeloraEngine --version)
endif()
