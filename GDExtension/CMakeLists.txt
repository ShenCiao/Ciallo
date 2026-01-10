cmake_minimum_required(VERSION 3.20)

project(ciallo_gdextension LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

set(GODOT_CPP_DIR "${CMAKE_CURRENT_SOURCE_DIR}/godot-cpp")
set(GODOT_CPP_BUILD_DIR "${CMAKE_CURRENT_BINARY_DIR}/godot-cpp")

if(NOT EXISTS "${GODOT_CPP_DIR}/CMakeLists.txt")
  message(FATAL_ERROR "godot-cpp submodule not found at ${GODOT_CPP_DIR}. Run `git submodule update --init --recursive`.")
endif()

if(CMAKE_BUILD_TYPE STREQUAL "Debug")
  set(GODOT_CPP_BUILD_TYPE "debug")
else()
  set(GODOT_CPP_BUILD_TYPE "release")
endif()

set(GODOT_CPP_INSTALL_HEADERS OFF CACHE BOOL "" FORCE)
set(GODOT_CPP_SYSTEM_HEADERS OFF CACHE BOOL "" FORCE)
set(GODOT_CPP_BUILD_DIR ${GODOT_CPP_BUILD_DIR} CACHE PATH "" FORCE)
set(GODOT_CPP_BUILD_TYPE ${GODOT_CPP_BUILD_TYPE} CACHE STRING "" FORCE)

add_subdirectory(${GODOT_CPP_DIR} ${GODOT_CPP_BUILD_DIR})

add_library(ciallo_gdextension SHARED
  src/example.cpp
  src/register_types.cpp
)

target_include_directories(ciallo_gdextension PRIVATE
  ${GODOT_CPP_DIR}/include
  ${GODOT_CPP_DIR}/include/godot_cpp
)

target_link_libraries(ciallo_gdextension PRIVATE godot-cpp)

if(MSVC)
  target_compile_options(ciallo_gdextension PRIVATE /W4 /EHsc)
else()
  target_compile_options(ciallo_gdextension PRIVATE -Wall -Wextra -Wpedantic)
endif()

set_target_properties(ciallo_gdextension PROPERTIES OUTPUT_NAME "ciallo")

set(GDEXTENSION_BIN_DIR "${CMAKE_CURRENT_SOURCE_DIR}/bin")
add_custom_command(TARGET ciallo_gdextension POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E make_directory "${GDEXTENSION_BIN_DIR}"
  COMMAND ${CMAKE_COMMAND} -E copy "$<TARGET_FILE:ciallo_gdextension>" "${GDEXTENSION_BIN_DIR}/$<TARGET_FILE_NAME:ciallo_gdextension>"
)
