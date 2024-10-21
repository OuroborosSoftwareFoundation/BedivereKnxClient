﻿
Public Enum KnxGroupType As Integer
    Switch = 10
    Dimming = 20
    Value = 30
    EnableCtl = 40
    Scene = 50
End Enum

Public Enum GroupValueType As Integer
    Bool = 10
    [Byte] = 20
    BytePercent = 21
    ByteArray = 30
End Enum

Public Enum KnxObjectPart As Integer
    Switch = 1
    Value = 2
End Enum

Public Enum KnxObjectPartPoint As Integer
    Control = 0
    Feedback = 1
End Enum

Public Enum KnxScheduleTimerState As Integer
    Stoped = 0
    Starting = 2
    Running = 1
End Enum

Public Enum KnxMessageType As Integer
    System = 0
    FromBus = 1
    ToBus = 2
End Enum