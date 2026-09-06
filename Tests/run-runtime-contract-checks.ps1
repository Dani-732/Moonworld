param([string]$RimWorldPath = 'G:\steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$managed = Join-Path $RimWorldPath 'RimWorldWin64_Data\Managed'
$harmonyDir = 'G:\steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldContracts-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput /reference:"$harmonyDir\0Harmony.dll" (Join-Path $PSScriptRoot 'RuntimeContractChecks.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Runtime contract check compilation failed' }
    & $testOutput (Join-Path $projectRoot '1.6\Assemblies') $managed $harmonyDir (Join-Path $RimWorldPath 'Mods\HolyGrailWarTest\1.6\Assemblies')
    if ($LASTEXITCODE -ne 0) { throw 'Runtime contract checks failed' }

    $mwXmlFiles = Get-ChildItem (Join-Path $projectRoot '1.6') -Filter '*.xml' -Recurse
    [xml]$mwTravel = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_TravelPrana.xml') -Raw
    if ($mwTravel.SelectSingleNode('/Defs/StatDef[defName="MW_SeparatedSustainMultiplier"]/defaultBaseValue').InnerText -ne '2' -or
        $mwTravel.SelectSingleNode('/Defs/HediffDef[defName="MW_TestIndependentSustain"]/stages/li/statFactors/MW_SeparatedSustainMultiplier').InnerText -ne '0.5') {
        throw 'Separated threshold must default to 2 with a debug-only native stat modifier fixture'
    }
    foreach ($mwXmlFile in $mwXmlFiles) { [xml](Get-Content -LiteralPath $mwXmlFile.FullName -Raw) | Out-Null }
    [xml]$mwQuestXml = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_HolyGrailWarQuest.xml') -Raw
    $mwQuestDef = $mwQuestXml.SelectSingleNode('/Defs/QuestScriptDef[defName="MW_HolyGrailWarQuest"]')
    if ($null -eq $mwQuestDef -or $mwQuestDef.randomlySelectable -ne 'false' -or
        $mwQuestDef.hideOnCleanup -ne 'false' -or $mwQuestDef.endOnColonyMove -ne 'false' -or $mwQuestDef.defaultCharity -ne 'false' -or
        $mwQuestDef.root.Class -ne 'QuestNode_Sequence' -or $mwQuestDef.successHistoryEvent -or
        $mwQuestDef.failedOrExpiredHistoryEvent) { throw 'War quest must have its own non-random, visible historical root without charity events' }
    [xml]$mwWorkshop = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_WarWorkshop.xml') -Raw
    $mwSite = $mwWorkshop.SelectSingleNode('/Defs/WorldObjectDef[defName="MW_WarWorkshop"]')
    $mwPart = $mwWorkshop.SelectSingleNode('/Defs/SitePartDef[defName="MW_WarWorkshopPart"]')
    if ($mwSite.ParentName -ne 'StaticWorldObjectBase' -or $mwSite.canHaveMap -ne 'true' -or
        $mwSite.worldObjectClass -ne 'MoonWorld.Site_WarWorkshop' -or $mwPart.workerClass -ne 'SitePartWorker' -or
        $mwPart.disallowsAutomaticDetectionTimerStart -ne 'true') { throw 'Workshop map or safe native site configuration missing' }
    foreach ($mwComp in @('WorldObjectCompProperties_FormCaravan','WorldObjectCompProperties_TimedDetectionRaids','WorldObjectCompProperties_EnterCooldown')) {
        if ($mwSite.SelectNodes("comps/li[@Class='$mwComp']").Count -ne 1) { throw "Workshop missing native component: $mwComp" }
    }
    $mwOutpostStep = $mwWorkshop.SelectSingleNode('/Defs/GenStepDef[defName="MW_WarWorkshopOutpost"]')
    if ($mwOutpostStep.linkWithSite -ne 'MW_WarWorkshopPart' -or $mwOutpostStep.genStep.Class -ne 'GenStep_Outpost' -or
        $mwOutpostStep.genStep.settlementDontGeneratePawns -ne 'true' -or $mwOutpostStep.genStep.generateLoot -ne 'false') {
        throw 'Workshop must reuse the native Outpost generator without new defenders or repeat loot'
    }
    [xml]$mwOutpost = Get-Content (Join-Path $RimWorldPath 'Data\Core\Defs\Sites\Parts\Outpost.xml') -Raw
    $mwNativePart = $mwOutpost.SelectSingleNode('/Defs/SitePartDef[defName="Outpost"]')
    if ($mwPart.siteTexture -ne $mwNativePart.siteTexture -or $mwPart.expandingIconTexture -ne $mwNativePart.expandingIconTexture) {
        throw 'Workshop texture does not match verified native Outpost assets'
    }
    [xml]$mwAbility = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_NoblePhantasms.xml') -Raw
    $mwBurst = $mwAbility.SelectSingleNode('/Defs/AbilityDef[defName="MW_TestPranaBurst"]')
    if ($mwBurst.gizmoClass -or $mwBurst.displayGizmoWhileUndrafted -ne 'true' -or $mwBurst.disableGizmoWhileUndrafted -ne 'false') {
        throw 'Test ability must use the native command and remain available undrafted'
    }
    [xml]$mwBridge = Get-Content (Join-Path $projectRoot '1.6\Patches\MW_HolyGrailWarBridge.xml') -Raw
    $mwDefs = New-Object System.Xml.XmlDocument
    $mwDefs.LoadXml('<Defs />')
    foreach ($mwSourceFile in Get-ChildItem (Join-Path $RimWorldPath 'Mods\HolyGrailWarTest\1.6\Defs') -Filter '*.xml' -Recurse) {
        [xml]$mwSource = Get-Content -LiteralPath $mwSourceFile.FullName -Raw
        foreach ($mwNode in $mwSource.DocumentElement.ChildNodes) {
            if ($mwNode.NodeType -eq [System.Xml.XmlNodeType]::Element) {
                $mwDefs.DocumentElement.AppendChild($mwDefs.ImportNode($mwNode, $true)) | Out-Null
            }
        }
    }
    if ($mwDefs.SelectNodes($mwBridge.Patch.Operation.xpath).Count -ne 1) { throw 'Installed prototype bridge target missing or ambiguous' }
    if ($mwBridge.SelectNodes('//value/li[@Class="MoonWorld.CompProperties_ServantCommands"]').Count -ne 1) {
        throw 'Servant presentation component missing or duplicated'
    }
    [xml]$mwEntry = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_HolyGrailWarEntry.xml') -Raw
    [xml]$mwClassic = Get-Content (Join-Path $RimWorldPath 'Data\Core\Defs\Scenarios\Scenarios_Classic.xml') -Raw
    $mwScenario = $mwEntry.SelectSingleNode('/Defs/ScenarioDef[defName="MW_HolyGrailWar"]')
    $mwParent = $mwClassic.SelectSingleNode('/Defs/ScenarioDef[@Name="Crashlanded"]')
    if ($mwScenario.ParentName -ne 'Crashlanded' -or $null -eq $mwParent -or
        $mwScenario.label -notmatch '[\u4e00-\u9fff]' -or $mwScenario.description -notmatch '[\u4e00-\u9fff]' -or
        $mwScenario.scenario.summary -notmatch '[\u4e00-\u9fff]') {
        throw 'Chinese scenario content or installed scenario parent missing'
    }
    if ($mwParent.SelectSingleNode('scenario/parts/li[@Class="ScenPart_ConfigPage_ConfigureStartingPawns"]/pawnCount').InnerText -ne '3' -or
        $mwScenario.SelectNodes('scenario/parts/li[@Class="MoonWorld.ScenPart_HolyGrailWar"][def="MW_HolyGrailWarStart"]').Count -ne 1 -or
        $mwEntry.SelectSingleNode('/Defs/ScenPartDef[defName="MW_HolyGrailWarStart"]/scenPartClass').InnerText -ne 'MoonWorld.ScenPart_HolyGrailWar' -or
        $mwEntry.SelectSingleNode('/Defs/LetterDef[defName="MW_HolyGrailWarInvitation"]/letterClass').InnerText -ne 'MoonWorld.ChoiceLetter_HolyGrailWar' -or
        $mwEntry.SelectSingleNode('/Defs/IncidentDef[defName="MW_HolyGrailWarInvitation"]/workerClass').InnerText -ne 'MoonWorld.IncidentWorker_HolyGrailWarInvitation') {
        throw 'Scenario or invitation Def linkage invalid'
    }
    $mwRaid = $mwEntry.SelectSingleNode('/Defs/IncidentDef[defName="MW_HolyGrailWarEnemyServantRaid"]')
    if (($null -eq $mwRaid) -or ($mwRaid.workerClass -ne 'MoonWorld.IncidentWorker_EnemyServantRaid') -or ($mwRaid.targetTags.li -ne 'Map_PlayerHome') -or ($mwRaid.requireColonistsPresent -ne 'true')) {
        throw 'Enemy servant raid incident linkage invalid'
    }
    [xml]$mwTraits = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_MasterCircuit.xml') -Raw
    if ($mwTraits.SelectSingleNode('/Defs/TraitDef[defName="MW_CommandSpell"]/commonality').InnerText -ne '0') {
        throw 'Command seals must not be granted by random trait generation'
    }
    [xml]$mwOpposition = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_WarOpposition.xml') -Raw
    if ([string]::IsNullOrWhiteSpace($mwOpposition.SelectSingleNode('/Defs/PawnKindDef[defName="MW_EnemyMaster"]/initialResistanceRange').InnerText)) {
        throw 'Enemy humanlike master requires an initial resistance range'
    }
    [xml]$mwNeeds = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_Needs_Hediffs.xml') -Raw
    foreach ($mwNeed in $mwNeeds.SelectNodes('/Defs/NeedDef | /Defs/HediffDef')) {
        if ([string]::IsNullOrWhiteSpace($mwNeed.description)) { throw "Missing description: $($mwNeed.defName)" }
    }
    [xml]$mwLegacy = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_SummonedServants.xml') -Raw
    foreach ($mwItem in $mwLegacy.SelectNodes('/Defs/ThingDef')) {
        if ([string]::IsNullOrWhiteSpace($mwItem.statBases.Mass)) { throw "Missing authored mass: $($mwItem.defName)" }
        if ($mwItem.ParentName -eq 'ApparelBase' -and $mwItem.smeltable -ne 'false') {
            throw "Legacy apparel without material returns cannot be smeltable: $($mwItem.defName)"
        }
        if ($mwItem.ParentName -eq 'BaseWeapon' -and ([string]::IsNullOrWhiteSpace($mwItem.techLevel) -or $mwItem.techLevel -eq 'Undefined')) {
            throw "Missing weapon tech level: $($mwItem.defName)"
        }
    }
    $mwFaction = $mwOpposition.SelectSingleNode('/Defs/FactionDef[defName="MW_WarOpposition"]')
    if ($mwFaction.Name -ne 'MW_WarOpposition' -or $mwFaction.ParentName -ne 'FactionBase') {
        throw 'Class faction inheritance must resolve through the XML Name attribute, not defName'
    }
    foreach ($mwSeat in @('Saber','Archer','Lancer','Assassin','Caster','Rider','Berserker')) {
        $mwChild = $mwOpposition.SelectSingleNode("/Defs/FactionDef[defName='MW_WarOpposition_$mwSeat']")
        if ($null -eq $mwChild -or $mwChild.ParentName -ne 'MW_WarOpposition' -or
            $mwOpposition.SelectNodes("/Defs/FactionDef[@Name='$($mwChild.ParentName)']").Count -ne 1) {
            throw "Class faction parent missing or ambiguous: $mwSeat"
        }
    }
    if ($mwOpposition.SelectNodes('/Defs/DutyDef[defName="MW_EnemyServantAssault"]/thinkNode/subNodes/li[1][@Class="MoonWorld.JobGiver_EnemyServantAssault"]').Count -ne 1) {
        throw 'Enemy duty must run servant targeting before vanilla assault fallback'
    }
    if ($mwFaction.hidden -ne 'true' -or $mwFaction.permanentEnemy -ne 'true' -or
        $mwFaction.raidsForbidden -ne 'true' -or $mwFaction.startingCountAtWorldCreation -ne '0' -or
        $mwFaction.autoFlee -ne 'false' -or $mwFaction.pawnGroupMakers) {
        throw 'Enemy faction must be isolated from ordinary world generation and raids'
    }
    [xml]$mwIdentities = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_HolyGrailWarBridge.xml') -Raw
    [xml]$mwPools = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_SummonPools.xml') -Raw
    $mwSeats = [ordered]@{ Saber='Artoria'; Archer='Emiya'; Lancer='CuChulainn'; Assassin='Sasaki'; Caster='Medea'; Rider='Medusa'; Berserker='Heracles' }
    if ($mwPools.SelectNodes('/Defs/MoonWorld.ServantSummonPoolDef').Count -ne 7) { throw 'Expected seven class pool tables' }
    $mwTextures = Get-ChildItem (Join-Path $RimWorldPath 'Mods\HolyGrailWarTest\1.6\Textures') -File -Recurse
    foreach ($mwSeat in $mwSeats.Keys) {
        $mwKindName = 'GWW_' + $mwSeats[$mwSeat]
        $mwIdentity = $mwIdentities.SelectSingleNode("/Defs/Def[servantKind='$mwKindName']")
        $mwPool = $mwPools.SelectNodes("/Defs/MoonWorld.ServantSummonPoolDef[warClass='$mwSeat']")
        if ($mwIdentity.warClass -ne $mwSeat -or $mwIdentity.summonable -ne 'true' -or $mwPool.Count -ne 1 -or
            $mwPool[0].servants.li -ne $mwIdentity.defName) { throw "Invalid class pool/identity: $mwSeat" }
        $mwKind = $mwDefs.SelectSingleNode("/Defs/PawnKindDef[defName='$mwKindName']")
        $mwSourceIdentity = $mwDefs.SelectSingleNode("/Defs/HolyGrailWar.ServantIdentityDef[servantKind='$mwKindName']")
        if ($mwKind.race -ne 'GWW_HeroicSpirit' -or $null -eq $mwSourceIdentity -or
            [string]::IsNullOrWhiteSpace($mwSourceIdentity.fixedName)) { throw "Installed servant missing: $mwKindName" }
        $mwAssetNodes = @($mwSourceIdentity)
        foreach ($mwRef in $mwSourceIdentity.SelectNodes('requiredWeapon|requiredApparel|requiredAccessories/li|requiredInventory/li|requiredHair|requiredTraits/li|facialAnimationEyeColorMarker')) {
            if (-not $mwRef.InnerText.StartsWith('GWW_')) { continue }
            $mwAsset = $mwDefs.SelectSingleNode("/Defs/*[defName='$($mwRef.InnerText)']")
            if ($null -eq $mwAsset) { throw "Installed content reference missing: $($mwRef.InnerText)" }
            $mwAssetNodes += $mwAsset
        }
        foreach ($mwAsset in $mwAssetNodes) {
            foreach ($mwPath in $mwAsset.SelectNodes('.//texPath|.//wornGraphicPath|transparentBodyPath|fullBodyGraphicPath')) {
                if (-not $mwPath.InnerText.StartsWith('HolyGrailWar/')) { continue }
                $mwSuffix = $mwPath.InnerText.Replace('/', '\')
                if (-not ($mwTextures | Where-Object { $_.FullName -like "*\$mwSuffix.png" -or $_.FullName -like "*\${mwSuffix}_*.png" })) {
                    throw "Installed content texture missing: $($mwPath.InnerText)"
                }
            }
        }
    }
    Write-Host "$($mwXmlFiles.Count) XML files parsed; seven class pools, installed identities/loadout textures, native ability, Chinese scenario and isolated enemy faction checked."
}
finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
