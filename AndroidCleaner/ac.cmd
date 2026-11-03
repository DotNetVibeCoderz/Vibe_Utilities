@echo off
rem Shortcut: ac <command> [options]  ==  python -m androidcleaner <command> [options]
cd /d "%~dp0"
python -m androidcleaner %*
