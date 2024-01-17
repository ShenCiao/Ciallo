#pragma once
#include <iostream>
#include <memory>
#include <chrono>
namespace chrono = std::chrono;

#include <optional>
#include <vector>
#include <unordered_set>
#include <string>

// for sln
#include <glad/glad.h>
#include <imgui.h>
#include <imgui_internal.h>
#include <imgui_stdlib.h>
#include <entt/entt.hpp>

//for me
// #include "include/glad/glad.h" 
// #include "../imgui/imgui.h"
// #include "../imgui/imgui_internal.h"
// #include "../imgui/imgui_stdlib.h"
// #include "include/entt/entt.hpp"

#include <glm/glm.hpp>
#include <glm/gtc/type_ptr.hpp>
#include <fmt/ostream.h>
#include <fmt/ranges.h>
#include <spdlog/spdlog.h>

#include <ranges>
namespace views = std::views;
namespace ranges = std::ranges;
// -----------------------------------------------------------------------------
#include "Polyline.h"
#include "Registry.h"