#include "pch.hpp"
#include "AReadFile.h"

void AReadFile::ReadFile()
{
    // Specify the file path
    std::string path = "C:/Users/zguan/Desktop/stroke-universe/code/Ciallo/Ciallo/stamps";

    // Iterate over all the files in the directory
    for (const auto& entry : std::filesystem::directory_iterator(path))
    {
         //Extract the file name from the path and add it to the list
        file_names.push_back(entry.path().filename().string());
    }

    // Print the file names in the list
    //for (const auto& file_name : file_names)
    //{
    //    std::cout << file_name << std::endl;
    //}
}


