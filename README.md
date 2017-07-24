# Singular

This repository contains the source code for [Honorbuddy](http://www.honorbuddy.com/)'s default, all-in-one combat routine.

## Using Singular

Whenever a new version of Honorbuddy is released, the latest version of Singular is pulled from this repository, and included in the release automatically. 

If you want to obtain a copy of Singular manually, you can either download the contents of the repository as a .zip file, using the `Clone or download` -> `Download ZIP` buttons. Then delete the `Singular` directory inside Honorbuddy's `Routines` folder, and then extract the contents of the ZIP so that `C:\Path\to\HB\Routines\Singular\Singular.xml` is valid.

## Developing Singular

Since Honorbuddy compiles Singular by itself there is no need to set up
a proper build environment. However, this is still beneficial if you are going to
be making changes to Singular to make sure your changes still compile.

The repo includes at VS2017 solution which can be opened. To make the project compile
you must add references to Honorbuddy's `.exe` and `.dll` files. The project is already
set up to reference the correct assemblies in the `Dependencies` directory, so this
directory just needs to be created.

The easiest way to do that is with a symbolic link to your Honorbuddy installation. If
the path `C:\Path\to\Honorbuddy\Honorbuddy.exe` is valid, this is easily done by opening
a command prompt in the root of Singular (in the same folder as the `.sln` file)
and running the following command (if using PowerShell, you should prefix this command with
`cmd /c `):
```
mklink /J Dependencies "C:\Path\to\Honorbuddy"
```
Singular should now build successfully in VS2017.

## Contributing

See the [Contributing document](CONTRIBUTING.md) for guidelines for making contributions.

## Discuss

You can discuss Honorbuddy in our Discord channel which can be found [here](https://discordapp.com/invite/0q6seK1er9pqFZkZ).