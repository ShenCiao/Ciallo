#pragma once

#include "Window.h"

enum class PaintMode
{
	Illustration, // Layer system enabled, no timeline.
	Animation, // Layer system disabled, timeline enabled.
};

class Application
{
public:
	std::unique_ptr<Window> Window;
	PaintMode Mode = PaintMode::Illustration;
	bool ShowMetricsWindow = false;

	Application();

	void Run();
	void GenDefaultProject();
	void DrawMainMenu();
};
