# VBCat
Netcat style listener and remote connection tool written in VB.Net

Essentially [VBCat](https://github.com/MidwintersTomb/VBCat), with a bit of an upgrade.  First off, there's better error handling, so there should be less crashes.  Secondly, in the prior version of [VBCat](https://github.com/MidwintersTomb/VBCat), if you used -c it worked like a Windows run box, as any command you entered was passed to cmd /c and returned the result.  Now if you use the -c mode, you are able to set the process you want to use (cmd, powershell, etc.).  The old [VBCat](https://github.com/MidwintersTomb/VBCat) still has some niche use as you can launch multiple commands without hanging your connection (unless you launch something wanting input, like CMD, PowerShell, etc.), however it was extremely frustrating if you wanted to browse file structures, etc.  (Hence the creation of VBCat2.)

## Usage:
```
Usage: VBCat.exe <mode> [options]

Modes:

-c <hostname> <port> <program)    (Connect to a remote host)

-l <port>                         (Listen for incoming connections)
```

As per usual, I think it goes without saying, ***use at your own risk*** I'm not responsible for what you do with it.
