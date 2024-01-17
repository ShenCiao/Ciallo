#pragma once
#ifndef AREADFILE_
#define AREADFILE_

#include <iostream>
#include <string>
#include <filesystem>
#include <vector>

class AReadFile {
public:
    static inline std::vector<std::string> file_names;
    static void ReadFile();
};

#endif
