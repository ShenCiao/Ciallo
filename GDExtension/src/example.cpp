#include "example.h"

#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/variant/utility_functions.hpp>

using namespace godot;

void Example::_bind_methods() {
  ClassDB::bind_method(D_METHOD("say_hello"), &Example::say_hello);
}

void Example::say_hello() {
  UtilityFunctions::print("Ciallo GDExtension template says hello!");
}
