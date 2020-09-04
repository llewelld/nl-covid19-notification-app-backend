# NOT designed for Powershell ISE
# Double-check you are allowed to run custom scripts.

$TestfileName
$TestfileNameNoExt
$date
$VariablesSource = "Development"

if("#{Deploy.HSMScripting.OpenSslLoc}#" -like "*Deploy.HSMScripting.OpenSslLoc*")
{
	#ontw
    $OpenSslLoc = "`"C:\Program Files\OpenSSL-Win64\bin\openssl.exe`""
    $HSMAdminToolsDir = "C:\Program Files\Utimaco\CryptoServer\Administration"
    $SignerLoc = ".\Signer\SigTestFileCreator.exe"
    $Environment = "Ontw"
    
	$EcdsaCertThumbPrint = "Not Specified"
}
else
{
	#test, accp and prod
	$VariablesSource = "Deploy"
    $OpenSslLoc = "`"#{Deploy.HSMScripting.OpenSslLoc}#`""
    $HSMAdminToolsDir = "#{Deploy.HSMScripting.HSMAdminToolsDir}#"
    $SignerLoc = "`"#{Deploy.HSMScripting.VerifierLoc}#`""
	$TestfileName = "#{Deploy.HSMScripting.CertKeyExtractor.TestfileName}#"
    $TestfileNameNoExt = "#{Deploy.HSMScripting.CertKeyExtractor.TestfileNameNoExt}#"
    
    
	$Environment = "#{Deploy.HSMScripting.Environment}#"
    $EcdsaCertThumbPrint = "#{Deploy.HSMScripting.EcdsaCertThumbPrint}#"
}

function SetErrorToStop
{
	$ErrorAtStart = $ErrorActionPreference
	$ErrorActionPreference = "Stop"
	write-host "Error-behaviour is set from $ErrorAtStart to $ErrorActionPreference."
}

function CheckNotIse
{
	if($host.name -match "ISE")
	{
		write-host "`nYou are running this script in Powershell ISE. Please switch to the regular Powershell."
		Pause
		
		exit
	}
}

function CheckNotWin7
{
	if([int](Get-WmiObject Win32_OperatingSystem).BuildNumber -lt 9000)
	{
		write-warning "`nAutomated export of certificates is not possible under Windows 7!`nThis script will not function."
		Pause
		
		exit
	}
}

function Pause ($Message = "Press any key to continue...`n")
{
    If ($psISE) {
        # The "ReadKey" functionality is not supported in Windows PowerShell ISE.
 
        $Shell = New-Object -ComObject "WScript.Shell"
        $Button = $Shell.Popup("Click OK to continue.", 0, "Script Paused", 0)
 
        Return
    }
 
    Write-Host -NoNewline $Message
 
    $Ignore =
        16,  # Shift (left or right)
        17,  # Ctrl (left or right)
        18,  # Alt (left or right)
        20,  # Caps lock
        91,  # Windows key (left)
        92,  # Windows key (right)
        93,  # Menu key
        144, # Num lock
        145, # Scroll lock
        166, # Back
        167, # Forward
        168, # Refresh
        169, # Stop
        170, # Search
        171, # Favorites
        172, # Start/Home
        173, # Mute
        174, # Volume Down
        175, # Volume Up
        176, # Next Track
        177, # Previous Track
        178, # Stop Media
        179, # Play
        180, # Mail
        181, # Select Media
        182, # Application 1
        183  # Application 2
 
    While ($KeyInfo.VirtualKeyCode -Eq $Null -Or $Ignore -Contains $KeyInfo.VirtualKeyCode) {
        $KeyInfo = $Host.UI.RawUI.ReadKey("NoEcho, IncludeKeyDown")
    }
 
    Write-Host
}
# Got this from https://adamstech.wordpress.com/2011/05/12/how-to-properly-pause-a-powershell-script/
# A soviet-style pause function...

function RunWithErrorCheck ([string]$command) 
{
	iex "& $command"

    if($lastexitcode -ne 0)
    {
        write-Warning "Script terminated due to an error. :("
		Read-Host 'Press Enter to continue.'
        exit
    }
}

function GenTestFile
{
	$contentstring = read-host "Please input a test string that will be used for signing"
	$script:date = Get-Date -Format "MM_dd_HH-mm-ss"
	$fileContent = "Key verification file ($script:Environment)`r`n$contentstring"
	$script:TestfileNameNoExt = "KeyExtractionRawData"
	$script:TestfileName = "$script:TestfileNameNoExt.txt"
	
	New-Item -force -name ($script:TestfileName) -path ".\Temp$script:date\" -ItemType File -Value $fileContent -ErrorAction Stop
	New-Item -force -name "Result$script:date" -path "." -ItemType Directory -ErrorAction Stop
}

function ExtractKey ([String] $ThumbPrint, [String] $Store, [String] $ExportPath)
{
	$cert = Get-ChildItem -Path "cert:\LocalMachine\$Store\$ThumbPrint"
	#opvangbak prevents black magic from occurring.
	$opvangbak = Export-Certificate -Cert $cert -FilePath ".\Temp$script:date\$ExportPath.cer" -Type CERT
	
	# the exported cert is in der-format and needs to be converted to pem-format to use for signature verification
	RunWithErrorCheck "$openSslLoc x509 -in .\temp$script:date\$ExportPath.cer -inform der -pubkey -noout -out .\Temp$script:date\$script:Environment-Ecdsa.pub"
	RunWithErrorCheck "$openSslLoc ec -in .\Temp$script:date\$script:Environment-Ecdsa.pub -text -pubin -out .\Result$script:date\$script:Environment-Ecdsa.txt"
}

#
# Start
#


write-host "Certificate-Verifier"
write-warning "`nUsing variables from $VariablesSource`n"
Pause

CheckNotWin7

write-warning "`nDid you do the following?`
- Add the NEW Ecdsa thumbprint to this script?`
- Add the location of the signer to this script?`
- Add the Rsa (non-root)- and NEW Ecdsa thumbprint to the signer appsettings.json?`
If not: abort this script with ctrl+c."
Pause

SetErrorToStop
CheckNotIse

write-host "`nCheck if HSM is accessible"
Pause

RunWithErrorCheck "`"$HSMAdminToolsDir\cngtool`" providerinfo"

write-host "`nGenerating testfile"
Pause

gentestfile

write-host "`nExtracting public key"
Pause

ExtractKey -ThumbPrint $EcdsaCertThumbPrint -Store "my" -ExportPath "EcdsaCert"

write-host "`nSigning testfile with Verifier"
Pause

RunWithErrorCheck "$SignerLoc .\Temp$script:date\$TestfileName"

Expand-Archive -Force -LiteralPath ".\Temp$script:date\$testfileNameNoExt-eks.zip" -DestinationPath ".\Result$script:date\" -ErrorAction Stop

write-host "`nDone!`nYou can take export.bin, export.sig and the .key-file from the results folder now."
Pause
