#pragma once

inline entt::registry R{};

template <>
struct fmt::formatter<entt::entity> : formatter<uint32_t> {
    auto format(entt::entity e, fmt::format_context& ctx) const {
        return fmt::format_to(ctx.out(), "{}", static_cast<uint32_t>(e));
    }
};
