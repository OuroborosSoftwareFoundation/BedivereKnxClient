﻿Imports System.Data
Imports System.Net.NetworkInformation
Imports Knx.Falcon
Imports Knx.Falcon.Configuration
Imports Knx.Falcon.Sdk

''' <summary>
''' KNX总线
''' </summary>
Public Class KnxSystemBusCollection

    Implements IEnumerable

    Private _Table As DataTable
    Private _Item As New Dictionary(Of String, KnxBus)
    Private _DefaultId As Integer = -1 '默认的接口ID

    Public Event ConnectionChanged As EventHandler '接口连接状态变化事件
    Public Event GroupMessageReceived As KnxMessageHandler '组地址报文接收事件
    Protected Friend Event GroupPollRequest() '组地址轮询申请事件

    ''' <summary>
    ''' 默认接口（第一个IpRouting类型）
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property [Default] As KnxBus
        Get
            If _DefaultId < 0 Then
                Throw New NullReferenceException("Can't found default KNX bus.")
                Return Nothing
            Else
                Return Item(_DefaultId)
            End If
        End Get
    End Property

    ''' <summary>
    ''' 对象DataTable
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Table As DataTable
        Get
            Return _Table
        End Get
    End Property

    Default Public ReadOnly Property Item(index As Integer) As KnxBus
        Get
            Return Item(_Table.Rows(index)("InterfaceCode").ToString)
        End Get
    End Property

    Default Public ReadOnly Property Item(code As String) As KnxBus
        Get
            If _Item.Keys.Contains(code) Then
                Return _Item(code)
            Else
                Return [Default] '找不到接口编号的情况下直接引用默认接口
            End If
        End Get
    End Property

    Public Sub Add(code As String, bus As KnxBus)
        _Item.Add(code, bus)
    End Sub

    Public ReadOnly Property Count As Integer
        Get
            Return _Item.Count
        End Get
    End Property

    Public Sub New(dt As DataTable)
        If dt.Rows.Count = 0 Then
            'Throw New NullReferenceException($"No interface detected in data file.")
            Dim dr As DataRow = dt.NewRow
            dr("Id") = 0
            dr("InterfaceCode") = "Default"
            dr("InterfaceName") = "Default"
            dr("InterfaceType") = "IpRouter"
            dr("Address") = "224.0.23.12"
            dr("Port") = "3671"
            dt.Rows.Add(dr)
        End If
        _Table = dt
        With _Table
            '.PrimaryKey = { .Columns("InterfaceCode")}
            .Columns.Add("NetState", GetType(IPStatus)) '网络状态
            .Columns("NetState").Caption = "网络状态"
            .Columns.Add("CnState", GetType(BusConnectionState)) '接口连接状态
            .Columns("CnState").Caption = "连接状态"
            _Item.Clear()
            For Each dr As DataRow In _Table.Rows
                Dim cp As ConnectorParameters
                Select Case dr("InterfaceType").ToString.ToLower
                    Case "usb"
                        cp = New UsbConnectorParameters
                    Case "iptunnel"
                        cp = New IpTunnelingConnectorParameters(dr("Address"), dr("Port"))
                    Case "iprouter"
                        cp = New IpRoutingConnectorParameters(Net.IPAddress.Parse(dr("Address")))
                        If _DefaultId < 0 Then '只允许一个IpRouting接口，如果之前读过则报错
                            _DefaultId = dr("Id")
                        Else
                            Throw New ArgumentException($"Only ONE IpRouting interface is allowed.")
                        End If
                    Case Else '其他情况报错
                        Throw New ArgumentNullException($"Invalid interface type: '{dr("InterfaceType")}'.")
                End Select
                cp.Name = dr("InterfaceCode")
                cp.AutoReconnect = True '启用自动重连
                Dim k As New KnxBus(cp)
                AddHandler k.ConnectionStateChanged, AddressOf _ConnectionChanged
                AddHandler k.GroupMessageReceived, AddressOf _GroupMessageReceived
                dr("NetState") = IPStatus.Unknown
                dr("CnState") = k.ConnectionState '初始化连接状态
                _Item.Add(dr("InterfaceCode").ToString, k)
            Next
        End With
    End Sub

    Public Ready As Boolean = False '总线就绪
    Private ReadOnly _Default As KnxBus

    ''' <summary>
    ''' 打开全部接口
    ''' </summary>
    Public Sub AllConnect(Optional GroupPoll As Boolean = False)
        Dim th As New Threading.Thread(Sub() _AllConnect(GroupPoll)) '新建线程打开KNX接口
        th.Start() '启动新线程
    End Sub

    Private Async Sub _AllConnect(Optional GroupPoll As Boolean = False)
        Ready = False
        For Each dr As DataRow In _Table.Rows
            Try
                If dr("CnState") = BusConnectionState.Closed Then '只处理Close状态的接口
                    Dim IfCode As String = dr("InterfaceCode").ToString
                    If dr("InterfaceType").ToString.ToLower.Contains("iptunnel") Then '网络接口
                        Dim p As New Ping
                        Dim pr As PingReply = p.Send(dr("Address").ToString, 100)
                        dr("NetState") = pr.Status
                        RaiseEvent ConnectionChanged(Nothing, Nothing) '触发事件
                        If pr.Status = IPStatus.Success Then
                            Await _Item(IfCode).ConnectAsync() '异步方式打开接口提高显示速度
                        End If
                    Else
                        Await _Item(IfCode).ConnectAsync() '异步方式打开接口提高显示速度
                    End If
                End If '跳过已经连接的接口
            Catch ex As Exception
                Throw
            End Try
        Next
        Ready = True
        If GroupPoll Then RaiseEvent GroupPollRequest()
    End Sub

    Private Sub _ConnectionChanged(sender As KnxBus, e As EventArgs)
        For Each dr In _Table.Rows
            dr("CnState") = _Item(dr("InterfaceCode")).ConnectionState
        Next
        RaiseEvent ConnectionChanged(sender, e) '触发事件
    End Sub

    Private Sub _GroupMessageReceived(sender As Object, e As GroupEventArgs)
        RaiseEvent GroupMessageReceived(New KnxMsgEventArgs(KnxMessageType.FromBus, e), vbNullString) '触发事件
    End Sub

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return _Item.Values.GetEnumerator()
        'Throw New NotImplementedException()
    End Function

End Class