<##############################

	NAME: NetTracker

	AUTHOR: Timokhin Valery
	DATE  : 21.07.2022

	DESCRIPTION:	     


	SETUP:
	   Скопировать в папку со скриптом файлы:
		   - [НЕОБЯЗАТЕЛЬНО] openvr_api.dll из steamVR или https://github.com/ValveSoftware/openvr.git
		   - openvr_api.cs из SteamVR\plugins
		   - актуализироваь список IP в блоке PUT HERE IP ADDRESSES

	EXAMPLES:




	TODO:	
	   
##############################>

using namespace Valve.VR
using namespace System.Net.Sockets
using namespace System.Net


$home_dir = (Split-Path $SCRIPT:MyInvocation.MyCommand.Path) -replace "\\$", ""	#Корневая директория сайта
cd $home_dir


# SERVER PARAMETERS

$script:Port = 8084

#    					 / PUT HERE IP ADDRESSES
#   					v 
$script:listeners = @("127.0.0.1", "192.168.0.100", "192.168.0.101", "192.168.0.102", "192.168.0.103", "10.12.248.147")  # TP-Link Router
$script:listeners = @("127.0.0.1", "172.29.16.165", "10.12.248.222", "172.29.16.165", "10.12.248.155", "10.12.248.154", "10.12.248.22", "172.29.16.32")

# Motion dectection when pose exceed the threshold
$script:jitter_threshold = 0.0001

# Load API

$openvr_api_cs = "openvr_api.cs"

if(! (Test-Path $openvr_api_cs) ){
	
	# try find 
	$openvr_api_cs = Join-Path "..\..\SteamVR\Plugins" "openvr_api.cs" -resolve	
}

[string]$openvr_api_cs = Get-Content $openvr_api_cs | Out-String

Add-Type -TypeDefinition $openvr_api_cs

# Load openvr_api.dll
if(! (Test-Path "openvr_api.dll")){
	
	$steamVR_dir = ""
	
	# Search path in APPDATA
	if( Test-Path "$Env:LOCALAPPDATA\openvr\openvrpaths.vrpath"){
		$steamVR_dir = (get-content "$Env:LOCALAPPDATA\openvr\openvrpaths.vrpath" | ConvertFrom-Json).runtime -as [string]
		$steamVR_dir += "\bin\win64\"
	}
	
	# Search path by vrserver process
	else{	
		$vrserver_process = (Get-Process vrserver).Path
		$steamVR_dir = [System.IO.Path]::GetDirectoryName($vrserver_process)
	}
	$env:Path = "$steamVR_dir;" + $env:Path
}



# Initialize OpenVR

$devices = @()

$_ovrTrackedDevicePoses = New-Object TrackedDevicePose_t[] ([OpenVR]::k_unMaxTrackedDeviceCount)

$script:_cvrSystem = $null


# VIVE TRACKER API

function init-openvr
{
	# OpenVR Init
	$_error = [EVRInitError]::None;
	$script:_cvrSystem = [OpenVR]::Init([ref] $_error, [Valve.VR.EVRApplicationType]::VRApplication_Other);
	if ($_error -ne [EVRInitError]::None)
	{
		Write-Output "OpenVR Error : $_error"
	}
	else
	{
		Write-Output  "OpenVR initialized."
	}
}

function get-sn{
	[CmdletBinding()]
    param(
        [uint32]$trackerIndex = 0
    )
	
	$sn = "";
	$_error = New-Object ETrackedPropertyError
	$sb = New-Object System.Text.StringBuilder;
	$script:_cvrSystem.GetStringTrackedDeviceProperty($trackerIndex, [ETrackedDeviceProperty]::Prop_SerialNumber_String, $sb, [OpenVR]::k_unMaxPropertyStringSize, [ref] $_error) | Out-Null
	
	if ($_error -eq [ETrackedPropertyError]::TrackedProp_Success)
	{
		$sn = $sb.ToString()
	}
	return $sn
}


[array]$script:trackers = @()

function get-trackerPose{

	if ($_cvrSystem -eq $null) { 
		Write-Host "OpenVR not initialized! Try start SteamVR application"
		Exit 1
		return 
	}
	
	$_error = New-Object ETrackedPropertyError

	# Fetch last Vive Tracker devices poses.
	
	$_cvrSystem.GetDeviceToAbsoluteTrackingPose([ETrackingUniverseOrigin]::TrackingUniverseRawAndUncalibrated, 0, $_ovrTrackedDevicePoses)
	
	$trackerInfo = @{
		vr_trackers = @()
	}	
	
	# Apply poses to ViveTracker objects.
	for ($device_id = 0; $device_id -lt [OpenVR]::k_unMaxTrackedDeviceCount; ++$device_id)
	{
		$deviceRole = $_cvrSystem.GetInt32TrackedDeviceProperty($device_id, [ETrackedDeviceProperty]::Prop_ControllerRoleHint_Int32, [ref] $_error)
		$deviceClass = $_cvrSystem.GetTrackedDeviceClass($device_id)
		
		if ($deviceClass -notin ([ETrackedDeviceClass]::GenericTracker, [ETrackedDeviceClass]::Controller) )
		{
			continue
		}
		
		$sn = get-sn -trackerIndex $device_id			
		
		$state = New-Object VRControllerState_t
		$size = [System.Runtime.InteropServices.Marshal]::SizeOf($state)
		
		$_cvrSystem.GetControllerState($device_id, [ref] $state, $size) | Out-Null

		$pose = $_ovrTrackedDevicePoses[$device_id]				
		
		$matrix = $pose.mDeviceToAbsoluteTracking
		
		$m = @( 
			@( $matrix.m0, $matrix.m1, $matrix.m2, $matrix.m3 ),
			@( $matrix.m4, $matrix.m5, $matrix.m6, $matrix.m7 ),
			@( $matrix.m8, $matrix.m9, $matrix.m10, $matrix.m11 ))

		$trackerPose = @{
			serial = $sn;
			isConnected = $pose.bDeviceIsConnected;
			isValid = $pose.bPoseIsValid;
			TrackingResult = $pose.eTrackingResult;
			buttonState = $state.ulButtonPressed;
			class = $deviceClass;
			role = $deviceRole;
			pose = $m;			
		}
		
		# don't send equal states
		if($script:trackers.Count -gt 0)
		{
			$tracker = $script:trackers[$device_id]
			
			$areEqual = $tracker -ne $null -and (areEqual $m $tracker.pose $script:jitter_threshold)
			$areEqual = $areEqual -and ($tracker.buttonState -eq $state.ulButtonPressed)
			
			if($areEqual){
			
				$script:skipped++
				continue 
			}
			else
			{
				$script:trackers[$device_id] = $trackerPose
			}			
		}
		else
		{
			$script:trackers = @($null) * $_ovrTrackedDevicePoses.Count
		}
		
		
		$trackerPose += Get-Position $m
		$trackerPose += Get-Rotation $m

		$trackerInfo.vr_trackers += $trackerPose
	}	

	return $trackerInfo
}

function areEqual($m, $m2, $epsilon = 0.0001){	
	
	# Calculate distance between vectors formed by columns from two matrices
	# When the distance is greater than eps_sqr - matrices are not equal
	
	$eps_sqr = $epsilon * $epsilon
	
	for($column = 0; $column -lt 4; $column++)
	{
		$x = $m[0][$column] - $m2[0][$column]
		$y = $m[1][$column] - $m2[1][$column]
		$z = $m[2][$column] - $m2[2][$column]
		
		$diff = $x * $x + $y * $y + $z * $z
				
		if($diff -gt $eps_sqr) {
			return $false
		}				
	}
	
	return $true
}

function Get-Rotation($m) {
	$q = @{}
	
	$q.qw = [math]::Sqrt([Math]::Max(0.0, 1.0 + $m[0][0] + $m[1][1] + $m[2][2])) / 2.0;
	$q.qx = [math]::Sqrt([Math]::Max(0.0, 1.0 + $m[0][0] - $m[1][1] - $m[2][2])) / 2.0;
	$q.qy = [math]::Sqrt([Math]::Max(0.0, 1.0 - $m[0][0] + $m[1][1] - $m[2][2])) / 2.0;
	$q.qz = [math]::Sqrt([Math]::Max(0.0, 1.0 - $m[0][0] - $m[1][1] + $m[2][2])) / 2.0;
	$q.qx = copysign -sizeval $q.qx -signval ($m[2][1] - $m[1][2])
	$q.qy = copysign -sizeval $q.qy -signval ($m[0][2] - $m[2][0])
	$q.qz = copysign -sizeval $q.qz -signval ($m[1][0] - $m[0][1])
	
	return $q;
}

function Get-Position($m){
	return @{
		x = $m[0][3];
		y = $m[1][3];
		z = $m[2][3];
	}
}

function copysign {
	[CmdletBinding()]
    param(
        [float] $sizeval, 
		[float] $signval
    )
	
	
	if([Math]::Sign($signval) -eq 1)
	{
		return [Math]::Abs($sizeval)
	}
	else{
		return -[Math]::Abs($sizeval)
	}
}



# SERVER API

function Send-Data{
	[CmdletBinding()]
    param(		
        [string] $json_data = "{}"
    )		
	
	# Init endpoints
	if($script:isReady -eq $null){
	
		Write-Host  "Initializing started..."
	
		$script:endpoints = @()
		
		foreach($client in $script:listeners){
		
			$ip, $port = [array]$client.split(":")			
			
			if( [string]::IsNullOrEmpty($port) ) { $port = "2020" }
			
			$endpoint = New-Object System.Net.IPEndPoint -ArgumentList @([Net.IPAddress]::Parse($ip), [int]::Parse($port))				
			
			$script:endpoints += $endpoint			
		}
		
		$script:udpClient = New-Object System.Net.Sockets.UdpClient		
		
		$script:isReady = $true
				
		Write-Host "Initializing is ready"
		Write-Host "`nThreshold Motion dectection: " $script:jitter_threshold "m"
		Write-Host "`nSending tracker poses to:"
		$script:listeners
		
		Write-Host "Press ESC to terminate..."
		
	}	
	
	# send data		
	
	$data = [Text.Encoding]::UTF8.GetBytes($json_data)
	
	foreach($endpoint in $script:endpoints){
				
		$script:udpClient.Send($data, $data.length, $endpoint) | Out-Null
	}
}

function start-listener{
	[CmdletBinding()]
    param(		
        [string] $port = "2020"
    )
	
	# Initialize
	if( $script:isServerReady -eq $null){
	
		Write-Host "Listener starting..."
		
		$script:server = New-Object System.Net.IPEndPoint([Net.IPAddress]::Any, $port)
		$script:udpClient = New-Object System.Net.Sockets.UdpClient $port		
	}
	
	Write-Host "Listener is started. Port $port"
	
	# Receive Data
	while($true){
	
		Write-Host "Wait for recieve data..."
		
		$rawData = $script:udpClient.Receive([ref] $script:server)
		
		$content = [Text.Encoding]::UTF8.GetString($rawData)
		
		Write-Host "Receive $content"
	}
}

cls
Write-Host "`n`n`n------- Net Tracker Server -------`n`n`n"

[System.Reflection.Assembly]::LoadWithPartialName("System.web.Extensions") | Out-Null
$json = New-Object System.web.Script.Serialization.JavaScriptSerializer

Write-Host "`$Revision: 13340 $".Replace("`$","")

init-openvr

$timer = [System.Diagnostics.Stopwatch]::StartNew()
$elapsed = [System.Diagnostics.Stopwatch]::StartNew()
$frames_count = 0
$script:skipped = 0

while($true)
{	
	$poses = get-trackerPose
	
	if($poses.vr_trackers.Count -ne 0){
	
		$data = $json.Serialize($poses)
		Send-Data -json_data $data				
	}
	
	$frames_count++
	
	if($timer.ElapsedMilliseconds -ge 2000){
		$duration = "{0:00}:{1:00}:{2:00}" -f $elapsed.Elapsed.Hours, $elapsed.Elapsed.Minutes, $elapsed.Elapsed.Seconds
		Write-Progress -Activity "Sending speed: $($frames_count / 2) / sec      Skipped: $script:skipped      Working: $duration"
		$timer.Restart()
		$frames_count = 0
		$script:skipped = 0
	}
		
	if([Console]::KeyAvailable){
		$key = [Console]::ReadKey()
		
		if($key.Key -eq 27){
			Write-Host "Terminating..."
			return
		}			
	}
}