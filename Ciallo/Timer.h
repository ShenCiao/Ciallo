class Timer{
    public:
        std::chrono::time_point<std::chrono::high_resolution_clock> timestamp;
        bool run(){
            auto currTime = std::chrono::high_resolution_clock::now();
            auto duration = currTime - timestamp;
            if(duration > std::chrono::milliseconds(2000)){
                ImGuiIO io = ImGui::GetIO();
                spdlog::info("ImGui FPS: {}", io.Framerate);
                return true;
            }
            return false;
        };
};