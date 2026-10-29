cmake_minimum_required(VERSION 3.25)
project(VeloraAudioNative LANGUAGES C)

include(FetchContent)
include(CTest)
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(VELORA_MINIAUDIO_SOURCE_DIR "" CACHE PATH "Existing miniaudio source directory (hash verified)")
if(NOT VELORA_MINIAUDIO_SOURCE_DIR)
    FetchContent_Declare(miniaudio
        GIT_REPOSITORY https://github.com/mackron/miniaudio.git
        GIT_TAG 9634bedb5b5a2ca38c1ee7108a9358a4e233f14d
        SOURCE_SUBDIR velora-header-only)
    FetchContent_MakeAvailable(miniaudio)
    set(VELORA_MINIAUDIO_SOURCE_DIR "${miniaudio_SOURCE_DIR}")
endif()

file(SHA256 "${VELORA_MINIAUDIO_SOURCE_DIR}/miniaudio.h" MINIAUDIO_SHA256)
if(NOT MINIAUDIO_SHA256 STREQUAL "ac7af4de748b7e26b777f37e01cee313a308a7296a3eb080e2906b320cc55c89")
    message(FATAL_ERROR "miniaudio.h does not match the audited 0.11.25 revision")
endif()

find_package(Threads REQUIRED)
add_library(VeloraAudioNative SHARED velora_audio_native.c)
target_include_directories(VeloraAudioNative PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}")
target_include_directories(VeloraAudioNative SYSTEM PRIVATE "${VELORA_MINIAUDIO_SOURCE_DIR}")
target_link_libraries(VeloraAudioNative PRIVATE Threads::Threads ${CMAKE_DL_LIBS})
if(UNIX AND NOT APPLE)
    target_link_libraries(VeloraAudioNative PRIVATE m)
endif()
set_target_properties(VeloraAudioNative PROPERTIES
    OUTPUT_NAME velora_audio_native C_VISIBILITY_PRESET hidden POSITION_INDEPENDENT_CODE ON)

if(BUILD_TESTING)
    add_executable(VeloraAudioNativeTests ../../tests/VeloraAudioNativeTests.c)
    target_link_libraries(VeloraAudioNativeTests PRIVATE VeloraAudioNative)
    add_test(NAME VeloraAudioNativeTests COMMAND VeloraAudioNativeTests)
endif()

install(TARGETS VeloraAudioNative LIBRARY DESTINATION . RUNTIME DESTINATION .)
install(FILES "${VELORA_MINIAUDIO_SOURCE_DIR}/LICENSE" DESTINATION licenses RENAME miniaudio.txt)
