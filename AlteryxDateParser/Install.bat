﻿@echo off
:: BatchGotAdmin
:-------------------------------------
REM  --> Check for permissions
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"
REM --> If error flag set, we do not have admin.
if '%errorlevel%' NEQ '0' (
	echo Requesting administrative privileges...
	goto UACPrompt
) else ( goto gotAdmin )
:UACPrompt
	echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
	echo UAC.ShellExecute "%~s0", "", "", "runas", 1 >> "%temp%\getadmin.vbs"
	"%temp%\getadmin.vbs"
	exit /B
:gotAdmin
	pushd "%~dp0"
	echo [Settings] > "JDTools.ini"
	echo x64Path=%~dp0% >> "JDTools.ini"
	echo x86Path=%~dp0% >> "JDTools.ini"
	echo ToolGroup=JDTools >> "JDTools.ini"
	xcopy JDTools.ini "%ProgramFiles%\Alteryx\Settings\AdditionalPlugins\" /Y
	popd
	pause
:--------------------------------------
