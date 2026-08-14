set(SDL3_VERSION "3.5.0")

if(NOT TARGET SDL3::Headers)
    add_library(SDL3::Headers INTERFACE IMPORTED)
    set_property(TARGET SDL3::Headers PROPERTY
        INTERFACE_LINK_LIBRARIES SDL3_Headers)
endif()

if(NOT TARGET SDL3::SDL3-static)
    add_library(SDL3::SDL3-static INTERFACE IMPORTED)
    set_property(TARGET SDL3::SDL3-static PROPERTY
        INTERFACE_LINK_LIBRARIES SDL3-static)
endif()

if(NOT TARGET SDL3::SDL3)
    add_library(SDL3::SDL3 INTERFACE IMPORTED)
    set_property(TARGET SDL3::SDL3 PROPERTY
        INTERFACE_LINK_LIBRARIES SDL3-static)
endif()

set(SDL3_Headers_FOUND TRUE)
set(SDL3_SDL3_FOUND TRUE)
set(SDL3_SDL3-static_FOUND TRUE)
set(SDL3_FOUND TRUE)
