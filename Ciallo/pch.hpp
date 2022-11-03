#pragma once
#include <iostream>
#include <memory>

#include <optional>
#include <vector>
#include <unordered_set>
#include <string>
#include <format>

#include <gl/glew.h>
#include <glm/glm.hpp>
#include <spdlog/spdlog.h>
#include <imgui.h>
#include <imgui_internal.h>
// -----------------------------------------------------------------------------
#include <entt/entt.hpp>
using namespace entt::literals;
template <>
struct std::formatter<entt::entity> {
    constexpr auto parse(std::format_parse_context& ctx) {
        return ctx.begin();
    }

    auto format(entt::entity id, std::format_context& ctx) const {
        return std::format_to(ctx.out(), "{}", static_cast<uint32_t>(id));
    }
};
// -----------------------------------------------------------------------------
#include "GeometryPrimitives.h"
#include "EntityObject.h"