# Pty.Net package

This repository includes the .nupkg file for Pty.Net, a library used to communicate with processes under Linux.

This package has been altered to remove the 3 calls to Console.WriteLine that would mess with the ServerShell code / UI.

If you don't trust it, the package can be replaced with it's original one but you may have to resize the shell to fix it's UI when using the program.

All the original code from this library is Copyright (c) Microsoft Corporation under the [MIT License](https://github.com/microsoft/vs-pty.net/blob/main/LICENSE).