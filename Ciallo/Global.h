#pragma once

/*
 * Get current active registry. It's owned by current active project.
 */
entt::registry& GetRegistry();

/*
 * Get active registry. It's supposed to be active project's registry.
 */
void SetRegistry(entt::registry& registry);