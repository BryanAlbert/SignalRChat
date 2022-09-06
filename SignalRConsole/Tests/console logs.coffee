Both on Console, Old Mia joins:
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0]
      Executing endpoint '/chathub'

Old Mia joins her channels:
Mia joined the channel: mia@albertnet.us Mia
mia38308-9a9b-4a6b-9db9-9e9b6238283f joined the channel: mia38308-9a9b-4a6b-9db9-9e9b6238283f
mia38308-9a9b-4a6b-9db9-9e9b6238283f joined the channel: mia38308-9a9b-4a6b-9db9-9e9b6238283f Chat
mia38308-9a9b-4a6b-9db9-9e9b6238283f joined the channel: brucef68-3c37-4aef-b8a6-1649659bbbc4

Old Mia sends Hello null to the generic Mia channel in response to her joining that channel:
mia38308-9a9b-4a6b-9db9-9e9b6238283f sent this command to Mia on the channel mia@albertnet.us Mia: {"Command":"Hello","Racer":{"Blocked":null,"BluetoothId":null,"BluetoothDeviceName":null,"DeviceId":"conmia08-9a9b-4a6b-9db9-9e9b6238283f","Id":"mia38308-9a9b-4a6b-9db9-9e9b6238283f","Email":"mia@albertnet.us","Name":"Mia Albert","Handle":"Mia","Color":"Turquoise","Created":"2022-06-27T23:00:00","Modified":"2022-09-02T04:21:28"},"Merge":null,"Flag":null}

New Mia comes online and joins the generic Mia channel:
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0]
      Executing endpoint '/chathub'
Mia joined the channel: mia@albertnet.us Mia

New Mia joins the rest of her channels:
connew65-1468-4409-a21a-f5b4f000ee4f joined the channel: connew65-1468-4409-a21a-f5b4f000ee4f
connew65-1468-4409-a21a-f5b4f000ee4f joined the channel: connew65-1468-4409-a21a-f5b4f000ee4f Chat

Someone (New Mia) joined the generic Mia channel so Old Mia sends Hello null in response:
mia38308-9a9b-4a6b-9db9-9e9b6238283f sent this command to Mia on the channel mia@albertnet.us Mia: {"Command":"Hello","Racer":{"Blocked":null,"BluetoothId":null,"BluetoothDeviceName":null,"DeviceId":"conmia08-9a9b-4a6b-9db9-9e9b6238283f","Id":"mia38308-9a9b-4a6b-9db9-9e9b6238283f","Email":"mia@albertnet.us","Name":"Mia Albert","Handle":"Mia","Color":"Turquoise","Created":"2022-06-27T23:00:00","Modified":"2022-09-02T04:21:28"},"Merge":null,"Flag":null}

Someone joined the generic Mia channel (it was New Mia, but we can`t tell) so New Mia on QKR sends Hello null to the generic channel...
connew65-1468-4409-a21a-f5b4f000ee4f sent this command to Mia on the channel mia@albertnet.us Mia: {"Command":"Hello","Racer":{"Blocked":null,"BluetoothId":null,"BluetoothDeviceName":null,"DeviceId":"connew65-1468-4409-a21a-f5b4f000ee4f","Id":"connew65-1468-4409-a21a-f5b4f000ee4f","Email":"mia@albertnet.us","Name":"Mia Albert","Handle":"Mia","Color":"Yellow","Created":"2022-07-08T03:43:05","Modified":"2022-09-02T15:53:59"},"Merge":null,"Flag":null}

New Mia sends Merge to Old Mia`s DeviceId on Old Mia`s Id channel in response to her Hello null:
connew65-1468-4409-a21a-f5b4f000ee4f sent this command to conmia08-9a9b-4a6b-9db9-9e9b6238283f on the channel mia38308-9a9b-4a6b-9db9-9e9b6238283f: {"Command":"Merge","Racer":null,"Merge":{"DataVersion":1.1,"MergeIndex":{},"Friends":[],"Operators":[{"Name":"Addition","Tables":[]},{"Name":"Subtraction","Tables":[]},{"Name":"Multiplication","Tables":[]},{"Name":"Division","Tables":[]}],"DeviceId":"connew65-1468-4409-a21a-f5b4f000ee4f","Id":"connew65-1468-4409-a21a-f5b4f000ee4f","Email":"mia@albertnet.us","Name":"Mia Albert","Handle":"Mia","Color":"Yellow","Created":"2022-07-08T03:43:05","Modified":"2022-09-02T15:53:59"},"Flag":null}

Old Mia sends Merge to New Mia`s DeviceId on New Mia`s Id channel (they`re the same) in response to her Hello null:
mia38308-9a9b-4a6b-9db9-9e9b6238283f sent this command to connew65-1468-4409-a21a-f5b4f000ee4f on the channel connew65-1468-4409-a21a-f5b4f000ee4f: {"Command":"Merge","Racer":null,"Merge":{"DataVersion":1.1,"MergeIndex":{},"Friends":[{"Blocked":false,"BluetoothId":null,"BluetoothDeviceName":null,"DeviceId":null,"Id":"brucef68-3c37-4aef-b8a6-1649659bbbc4","Email":"bruce@hotmail.com","Name":"Bruce Albert","Handle":"Bruce","Color":"Red","Created":"2022-07-06T03:05:12","Modified":"2022-07-06T03:05:12"}],"Operators":[{"Name":"Addition","Tables":[]},{"Name":"Subtraction","Tables":[]},{"Name":"Multiplication","Tables":[{"Base":1,"Cards":[{"Fact":{"First":1,"Second":1},"Quizzed":3,"Correct":3,"TotalTime":1744,"BestTime":518,"MergeQuizzed":null,"MergeCorrect":null,"MergeTime":null},{"Fact":{"First":1,"Second":2},"Quizzed":3,"Correct":3,"TotalTime":2346,"BestTime":692,"MergeQuizzed":null,"MergeCorrect":null,"MergeTime":null}]},{"Base":2,"Cards":[{"Fact":{"First":2,"Second":1},"Quizzed":3,"Correct":3,"TotalTime":1770,"BestTime":542,"MergeQuizzed":null,"MergeCorrect":null,"MergeTime":null},{"Fact":{"First":2,"Second":2},"Quizzed":1,"Correct":1,"TotalTime":1532,"BestTime":532,"MergeQuizzed":null,"MergeCorrect":null,"MergeTime":null}]}]},{"Name":"Division","Tables":[]}],"DeviceId":"conmia08-9a9b-4a6b-9db9-9e9b6238283f","Id":"mia38308-9a9b-4a6b-9db9-9e9b6238283f","Email":"mia@albertnet.us","Name":"Mia Albert","Handle":"Mia","Color":"Turquoise","Created":"2022-06-27T23:00:00","Modified":"2022-09-02T04:21:28"},"Flag":null}

New Mia assumes Old Mia`s Id and joins the Id channel:
mia38308-9a9b-4a6b-9db9-9e9b6238283f joined the channel: mia38308-9a9b-4a6b-9db9-9e9b6238283f

Everyone exits:
mia38308-9a9b-4a6b-9db9-9e9b6238283f left the channel: brucef68-3c37-4aef-b8a6-1649659bbbc4
mia38308-9a9b-4a6b-9db9-9e9b6238283f left the channel: mia38308-9a9b-4a6b-9db9-9e9b6238283f
mia38308-9a9b-4a6b-9db9-9e9b6238283f left the channel: conmia08-9a9b-4a6b-9db9-9e9b6238283f
mia38308-9a9b-4a6b-9db9-9e9b6238283f left the channel: mia@albertnet.us Mia
mia38308-9a9b-4a6b-9db9-9e9b6238283f left the channel: mia38308-9a9b-4a6b-9db9-9e9b6238283f Chat
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[1]
      Executed endpoint '/chathub'
info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished in 311.0117ms 101
mia38308-9a9b-4a6b-9db9-9e9b6238283f left the channel: brucef68-3c37-4aef-b8a6-1649659bbbc4
mia38308-9a9b-4a6b-9db9-9e9b6238283f left the channel: mia38308-9a9b-4a6b-9db9-9e9b6238283f
mia38308-9a9b-4a6b-9db9-9e9b6238283f left the channel: connew65-1468-4409-a21a-f5b4f000ee4f
mia38308-9a9b-4a6b-9db9-9e9b6238283f left the channel: mia@albertnet.us Mia
mia38308-9a9b-4a6b-9db9-9e9b6238283f left the channel: mia38308-9a9b-4a6b-9db9-9e9b6238283f Chat
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[1]
      Executed endpoint '/chathub'
info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished in 99.4324ms 101

Dissect the json messages:
From Console:
connew65-1468-4409-a21a-f5b4f000ee4f sent this command to conmia08-9a9b-4a6b-9db9-9e9b6238283f on the channel mia38308-9a9b-4a6b-9db9-9e9b6238283f: 
{
    "Command": "Merge",
    "Racer": null,
    "Merge":
    {
        "DataVersion": "1.1",
        "MergeIndex": {},
        "Friends": [],
        "Operators": [
            {
                "Name": "Addition",
                "Tables": []
            },
            {
                "Name": "Subtraction",
                "Tables": []
            },
            {
                "Name": "Multiplication",
                "Tables": []
            },
            {
                "Name": "Division",
                "Tables": []
            }
        ],
    "DeviceId": "connew65-1468-4409-a21a-f5b4f000ee4f",
    "Id": "connew65-1468-4409-a21a-f5b4f000ee4f",
    "Email": "mia@albertnet.us",
    "Name": "Mia Albert",
    "Handle": "Mia",
    "Color": "Yellow",
    "Created": "2022-07-08T03:43:05",
    "Modified": "2022-09-02T15:53:59"}
    "Flag": null
}

