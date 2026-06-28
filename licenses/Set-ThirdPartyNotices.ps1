function Invoke-Main {
	$target = Join-Path $PSScriptRoot "..\THIRD-PARTY-NOTICES.txt"
	Set-Content -LiteralPath $target -Value "" -NoNewLine
	# Add notices for NuGet packages
	$runtimePackages = Get-RuntimePackages
	foreach ($package in $runtimePackages) {
		$license = Get-PackageLicense -Package $package
		Add-ThirdPartyNotice -Path $target -Package $package -License $license
	}
	# Add notices for manual-only entries
	$overrides = Get-ManualLicenseOverrides
	foreach ($entry in $overrides) {
		# Skip if the entry has already been added as NuGet package.
		$list = $runtimePackages | Where-Object { $_.Name -eq $entry.Name }
		if (($runtimePackages | Where-Object { $_.Name -eq $entry.Name } | Select-Object -First 1).Count -gt 0) {
			continue
		}
		# Create license information for manual-only entry
		$license = New-LicenseFromManualEntry $entry
		if ($null -eq $license) {
			Write-Error "No LicenseFile specified in entry of manual-licenses.json"
			continue
		}
		# Add notice for manual-only entry
		Add-ThirdPartyNotice -Path $target -Package $entry -License $license
	}
}
function Get-RuntimePackages {
	# Process for each project in given solution
	foreach ($projectPath in dotnet solution (Join-Path $PSScriptRoot "..") list | Select-Object -Skip 2) {
		# Report error and skip if project.assets.json does not exist
		$assetsPath = Join-Path (Split-Path $projectPath) "obj\project.assets.json"
		if (-not (Test-Path $assetsPath)) {
			Write-Error "project.assets.json not found: $assetsPath"
			continue
		}
		# Read and parse project.assets.json
		$assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
		# Skip if the project is test project
		if (($assets.project.frameworks.PSObject.Properties `
			| Where-Object { $_.Value.dependencies."Microsoft.NET.Test.Sdk" } `
			| Select-Object -First 1).Count -gt 0) {
			Write-Information "Skip test project: $projectPath"
			continue
		}
		# Collect all runtime packages in the project
		foreach ($tfm in $assets.targets.PSObject.Properties) {
			foreach ($asset in $tfm.Value.PSObject.Properties) {
				# Skip if the asset
				# - is not package,
				# - does not contain runtime definition, or
				# - does not contain non-`_._`-named files
				#   (where `_._` means no files)
				if ($asset.Value.type -ne "package" `
					-or -not $asset.Value.runtime `
					-or ($asset.Value.runtime.PSObject.Properties `
						| Where-Object { $_.Name -notmatch "/_\._$" }
						| Select-Object -First 1).Count -eq 0
					) {
					continue
				}
				# Send package name and version to caller
				$nameAndVersion = $asset.Name -split "/"
				[PSCustomObject]@{Name = $nameAndVersion[0]; Version = $nameAndVersion[1]}
			}
		}
	}
}
function Get-PackageLicense {
	param ($Package)
	# Load global packages directory
	if ($null -eq $script:GlobalPackagesDir) {
		$script:GlobalPackagesDir = (dotnet nuget locals global-packages --list) -replace "^global-packages:\s*", ""
	}
	# Load manual override
	if ($null -eq $script:ManualOverrides) {
		$script:ManualOverrides = Get-ManualLicenseOverrides
	}
	$override = $script:ManualOverrides `
		| Where-Object { $_.Name -eq $Package.Name -and $_.Version -eq $Package.Version } `
		| Select-Object -First 1
	if ($null -ne $override) {
		# Override license information
		$license = New-LicenseFromManualEntry $override
		if ($null -ne $license) {
			$license
			return
		}
	}
	$packageRootDir = Join-Path $script:GlobalPackagesDir $Package.Name.ToLower() $Package.Version
	$nuspec = [xml](Get-Content -LiteralPath (Join-Path $packageRootDir "$($Package.Name.ToLower()).nuspec") -Raw)
	$metadata = $nuspec.package.metadata
	if ($null -ne $metadata.license -and $metadata.license.type -eq "expression") {
		# SPDX
		$licenseId = $metadata.license."#text"
		$licenseText = Get-SpdxLicenseText $licenseId
	} else {
		if ($null -ne $metadata.license) {
			# License File
			$licenseResource = "$($metadata.license.'#text') in the package"
		} else {
			# License Url
			$licenseResource = $metadata.licenseUrl."#text"
		}
		Write-Warning "$($Package.Name) ($($Package.Version)): Unknown license found. " `
			+ "See $licenseResource and edit manual-licenses.json"
		$licenseId = "NO-SPDX ($licenseResource)"
		$licenseText = "<<<Edit manual-licenses.json to fill the license information.>>>"
	}
	# Detect ThirdPartyNotices-like files
	$thirdPartyNoticesFiles = Get-ChildItem -LiteralPath $packageRootDir `
		| Where-Object { $_.Name -imatch "third.*party.*notice" }
	if ($thirdPartyNoticesFiles.Count -gt 0) {
		# Usually only one, but take the first
		$thirdPartyNoticesText = Get-Content -LiteralPath $thirdPartyNoticesFiles[0].FullName -Raw
		# Combine license ID and text
		$licenseId = "$licenseId + THIRD-PARTY-NOTICES"
		$licenseText += "`n`n" + $thirdPartyNoticesText
	}
	[PSCustomObject]@{
		Id = $licenseId
		Text = $licenseText
	}
}
function Add-ThirdPartyNotice {
	param ($Path, $Package, $License)
	$sep = "-------------------------------------------------------------------------------"
	$text = "$sep`n"
	$text += "Package: $($Package.Name) ($($Package.Version))`n"
	$text += "License: $($License.Id)`n"
	if ($null -ne $Package.Source) {
		$text += "Source: $($Package.Source)`n`n"
	} else {
		$text += "Source: https://www.nuget.org/packages/$($Package.Name)/`n`n"
	}
	Add-Content -LiteralPath $Path -Value $text -NoNewLine
	Add-Content -LiteralPath $Path -Value $License.Text -NoNewLine
	Add-Content -LiteralPath $Path -Value "`n$sep`n`n" -NoNewLine
}
function New-LicenseFromManualEntry {
	param ($ManualEntry)
	$licenseText = $null
	if ($ManualEntry.LicenseId -match "^[A-Za-z0-9\.\-]+$") {
		$licenseText = Get-SpdxLicenseText $ManualEntry.LicenseId
	} elseif ($ManualEntry.LicenseFile) {
		$licenseText = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\licenses\manual" $ManualEntry.LicenseFile) -Raw
	}
	if ($null -eq $licenseText) {
		Write-Error "No LicenseFile specified in entry of manual-licenses.json"
		$null
		return
	}
	if ($null -ne $ManualEntry.Copyright) {
		$licenseText = $ManualEntry.Copyright + "`n`n" + $licenseText
	}
	[PSCustomObject]@{
		Id = $ManualEntry.LicenseId
		Text = $licenseText
	}
}
function Get-ManualLicenseOverrides {
	$manualJsonPath = Join-Path $PSScriptRoot "..\licenses\manual\manual-licenses.json"
	if (Test-Path $manualJsonPath) {
		return Get-Content -LiteralPath $manualJsonPath -Raw | ConvertFrom-Json
	}
	return @()
}
function Get-SpdxLicenseText {
	param ($LicenseId)
	if (-not $script:SpdxCache) {
		$script:SpdxCache = @{}
	}
	if ($script:SpdxCache.ContainsKey($LicenseId)) {
		return $script:SpdxCache[$LicenseId]
	}
	$text = Invoke-RestMethod "https://raw.githubusercontent.com/spdx/license-list-data/master/text/$LicenseId.txt"
	$script:SpdxCache[$LicenseId] = $text
	return $text
}

Invoke-Main
