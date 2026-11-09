import io
import sys

src = r"H:\Projects\RPBB-GCN-Recomp\work\runner-link-cmds.txt"
lines = io.open(src, encoding="utf-8", errors="replace").read().splitlines()
cmd = [l for l in lines if "moderngekko_run.cpp.obj" in l][0]

cmd = cmd.replace(
    '-DMODERNGEKKO_DEFAULT_WINDOW_TITLE="\\"SpongeBob SquarePants: Revenge of the Flying Dutchman\\""',
    '-DMODERNGEKKO_DEFAULT_WINDOW_TITLE="\\"ModernGekko - \\""')
cmd = cmd.replace(
    '-DMODERNGEKKO_REQUIRED_DISC_ID=\\"GSQE78\\" ', '')
cmd = cmd.replace(
    '-DMODERNGEKKO_REQUIRED_DOL_SHA256=\\"dca4f72b57e8bf3f540e694f01cc23ef687d2b25028d974b67356e15f1c2e009\\" ', '')
cmd = cmd.replace(
    '-DMODERNGEKKO_RUNNER_NAME=\\"SpongeBobRecompRunner\\"',
    '-DMODERNGEKKO_RUNNER_NAME=\\"moderngekko-run\\"')
cmd = cmd.replace(
    '-DMODERNGEKKO_USER_DIRECTORY_NAME=\\"SpongeBobRecomp\\"',
    '-DMODERNGEKKO_USER_DIRECTORY_NAME=\\"moderngekko\\"')
cmd = cmd.replace(
    '/FoCMakeFiles\\moderngekko-run.dir\\tools\\moderngekko_run.cpp.obj',
    '/FoCMakeFiles\\moderngekko-run.dir\\tools\\moderngekko_run_generic.cpp.obj')

if "REQUIRED_DISC_ID" in cmd or "REQUIRED_DOL_SHA256" in cmd:
    print("failed to strip defines", file=sys.stderr)
    sys.exit(1)

bat = r"""@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64 >nul
cd /d H:\Projects\SB-RFD\build\moderngekko
%s
exit /b %%ERRORLEVEL%%
""" % cmd

out = r"H:\Projects\RPBB-GCN-Recomp\tools\compile_generic_runner.bat"
io.open(out, "w", encoding="ascii", errors="replace").write(bat)
print("wrote", out, len(cmd))
