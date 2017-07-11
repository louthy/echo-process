dotnet restore
dotnet pack Echo.Process -c Release -o ../../artifacts/bin
dotnet pack Echo.Process.Redis -c Release -o ../../artifacts/bin
dotnet pack Echo.Process.Owin -c Release -o ../../artifacts/bin
dotnet pack Echo.ProcessJS -c Release -o ../../artifacts/bin
