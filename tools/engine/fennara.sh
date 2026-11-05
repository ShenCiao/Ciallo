# Fennara provisioning uses the project's required .NET SDK, with no NuGet dependencies.
fennara() {
    command -v dotnet >/dev/null || fail 'Install the .NET 10 SDK.'
    local options=()
    if $ci; then options+=(--ci); fi
    dotnet run --project "$(native_path "$script_dir/fennara/Fennara.csproj")" \
        --configuration Release --no-launch-profile -- \
        "$(native_path "$root")" "$rid" "${@:-sync}" "${options[@]}"
}
