$ErrorActionPreference='Stop'
$taskRoot='C:/Program Files (x86)/Steam/steamapps/common/tModLoader'
[void][Reflection.Assembly]::LoadFrom("$taskRoot/Libraries/FNA/1.0.0/FNA.dll")
[void][Reflection.Assembly]::LoadFrom("$taskRoot/Libraries/ReLogic/1.0.0/ReLogic.dll")
$taskAssembly=[Reflection.Assembly]::LoadFrom("$taskRoot/tModLoader.dll")
$taskCodes=@{}
foreach($taskField in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')){$taskOp=$taskField.GetValue($null);$taskCodes[[int]$taskOp.Value -band 65535]=$taskOp}
function Show-TaskIL($taskMethod){
    "METHOD $($taskMethod.DeclaringType.FullName)::$($taskMethod.Name)"
    $taskBytes=$taskMethod.GetMethodBody().GetILAsByteArray();$taskAt=0
    while($taskAt -lt $taskBytes.Length){
        $taskStart=$taskAt;$taskCode=[int]$taskBytes[$taskAt++];if($taskCode -eq 254){$taskCode=65024+[int]$taskBytes[$taskAt++]};$taskOp=$taskCodes[$taskCode];$taskValue='';$taskLength=0
        switch($taskOp.OperandType.ToString()){
            InlineNone {$taskLength=0}
            ShortInlineI {$taskLength=1;$taskValue=$taskBytes[$taskAt]}
            ShortInlineVar {$taskLength=1;$taskValue=$taskBytes[$taskAt]}
            ShortInlineBrTarget {$taskLength=1;$taskValue=$taskBytes[$taskAt]}
            InlineVar {$taskLength=2;$taskValue=[BitConverter]::ToUInt16($taskBytes,$taskAt)}
            InlineI {$taskLength=4;$taskValue=[BitConverter]::ToInt32($taskBytes,$taskAt)}
            InlineBrTarget {$taskLength=4;$taskValue=[BitConverter]::ToInt32($taskBytes,$taskAt)}
            ShortInlineR {$taskLength=4;$taskValue=[BitConverter]::ToSingle($taskBytes,$taskAt)}
            InlineR {$taskLength=8;$taskValue=[BitConverter]::ToDouble($taskBytes,$taskAt)}
            InlineI8 {$taskLength=8;$taskValue=[BitConverter]::ToInt64($taskBytes,$taskAt)}
            InlineSwitch {$taskLength=4+4*[BitConverter]::ToInt32($taskBytes,$taskAt);$taskValue='switch'}
            InlineString {$taskLength=4;$taskValue=$taskMethod.Module.ResolveString([BitConverter]::ToInt32($taskBytes,$taskAt))}
            default {$taskLength=4;$taskToken=[BitConverter]::ToInt32($taskBytes,$taskAt);try{$taskMember=$taskMethod.Module.ResolveMember($taskToken);$taskValue="$($taskMember.DeclaringType.FullName)::$taskMember"}catch{$taskValue="token=$taskToken"}}
        }
        '{0:X4} {1} {2}' -f $taskStart,$taskOp.Name,$taskValue;$taskAt+=$taskLength
    }
}
foreach($taskSpec in @(
    @('Terraria.Main','DrawPlayers_BehindNPCs'),
    @('Terraria.Main','DrawPlayers_AfterProjectiles'),
    @('Terraria.Graphics.Shaders.ArmorShaderDataSet','Apply'),
    @('Terraria.Graphics.Shaders.ArmorShaderData','Apply')
)){
    $taskType=$taskAssembly.GetType($taskSpec[0]);if(-not $taskType){continue}
    foreach($taskMethod in $taskType.GetMethods([Reflection.BindingFlags]'Public,NonPublic,Static,Instance')|Where-Object Name -eq $taskSpec[1]){Show-TaskIL $taskMethod}
}
$taskAssembly.GetType('Terraria.Graphics.SpriteViewMatrix').GetFields([Reflection.BindingFlags]'Public,NonPublic,Instance') | Select-Object Name,FieldType
