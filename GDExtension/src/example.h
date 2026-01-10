#ifndef CIALLO_GDEXTENSION_EXAMPLE_H
#define CIALLO_GDEXTENSION_EXAMPLE_H

#include <godot_cpp/classes/node.hpp>

namespace godot {

class Example : public Node {
  GDCLASS(Example, Node)

public:
  Example() = default;
  ~Example() override = default;

  static void _bind_methods();
  void say_hello();
};

} // namespace godot

#endif // CIALLO_GDEXTENSION_EXAMPLE_H
