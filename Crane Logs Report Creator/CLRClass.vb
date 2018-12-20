﻿Imports ADODB
Imports Crane_Logs_Report_Creator
Imports CrystalDecisions.CrystalReports.Engine
Imports CrystalDecisions.Windows.Forms
Imports Reports
Imports Reports.ReportFunctions
Public Class CLRClass
    Implements IReportswSave
    Implements ICraneLogsReport

    Public Sub New(Registry As String, ByRef N4connection As ADODB.Connection, ByRef OPConnection As ADODB.Connection, Username As String)
        CLRVessel = New Vessel(Registry, N4connection)
        Crane = New List(Of Crane)
        CraneLogsData = New CraneLogsData
        ReportFunctions = New ReportFunctions(OPConnection, N4connection) 'so you dont need to explicitly include the connection as parameter
        Me.N4Connection = N4connection
        Me.OPConnection = OPConnection
        Me.UserName = Username

    End Sub

    Private Property UserName As String
    Private Property DateNow As Date
    Private Property ReportFunctions As ReportFunctions
    Public ReadOnly Property N4Connection As Connection Implements IReportswSave.N4Connection
    Public ReadOnly Property OPConnection As Connection Implements IReportswSave.OPConnection
    Public ReadOnly Property CLRVessel As Vessel Implements ICraneLogsReport.Vessel
    Public ReadOnly Property Crane As List(Of Crane) Implements ICraneLogsReport.Crane
    Public ReadOnly Property CraneLogsData As CraneLogsData Implements ICraneLogsReport.CraneLogsData
    Public Property LastPort As Object Implements ICraneLogsReport.LastPort
    Public Property NextPort As Object Implements ICraneLogsReport.NextPort

    Public ReadOnly Property TotalMoves As Double Implements ICraneLogsReport.TotalMoves
        Get
            For Each crn As Crane In Crane
                If Crane IsNot Nothing Then TotalMoves += crn.Moves.TotalMoves
            Next
            Return TotalMoves
        End Get
    End Property

    Public ReadOnly Property FirstMove As Date Implements ICraneLogsReport.FirstMove
        Get
            Return Crane.AsEnumerable.Where(Function(crn) crn IsNot Nothing).Min(Function(mve) mve.FirstMove)
        End Get
    End Property

    Public ReadOnly Property LastMove As Date Implements ICraneLogsReport.LastMove
        Get
            Return Crane.AsEnumerable.Where(Function(crn) crn IsNot Nothing).Max(Function(mve) mve.LastMove)
        End Get
    End Property

    Public ReadOnly Property CraneDensity As Double Implements ICraneLogsReport.CraneDensity
        Get
            Return TotalMoves / Crane.AsEnumerable.Where(Function(crn) crn IsNot Nothing).Max(Function(mve) mve.Moves.TotalMoves)
        End Get
    End Property

    Public ReadOnly Property TotalBerthHours As Double Implements ICraneLogsReport.TotalBerthHours
        Get
            With CLRVessel
                Return GetSpanHours(.ATA, .ATD)
            End With
        End Get
    End Property

    Public ReadOnly Property NetBerthHours As Double Implements ICraneLogsReport.NetBerthHours
        Get
            Return TotalBerthHours - CraneLogsData.BerthingHourDelays.Totalhours
        End Get
    End Property

    Public ReadOnly Property GrossBerthProdRate As Double Implements ICraneLogsReport.GrossBerthProdRate
        Get
            Return TotalMoves / TotalBerthHours
        End Get
    End Property

    Public ReadOnly Property NetBerthProdRate As Double Implements ICraneLogsReport.NetBerthProdRate
        Get
            Return TotalMoves / NetBerthHours
        End Get
    End Property

    Public ReadOnly Property GrossVesselWorkingTime As Double Implements ICraneLogsReport.GrossVesselWorkingTime
        Get
            Return GetSpanHours(FirstMove, LastMove)
        End Get
    End Property

    Public ReadOnly Property NetVesselWorkingTime As Double Implements ICraneLogsReport.NetVesselWorkingTime
        Get
            NetVesselWorkingTime = GrossVesselWorkingTime
            For Each crn As Crane In Crane
                If Crane IsNot Nothing Then NetVesselWorkingTime -= (crn.Delays.Deductable.Totalhours + crn.Delays.Break.Totalhours)
            Next
            Return NetVesselWorkingTime
        End Get
    End Property

    Public ReadOnly Property GrossVesselProdRate As Double Implements ICraneLogsReport.GrossVesselProdRate
        Get
            Return TotalMoves / GrossVesselWorkingTime
        End Get
    End Property

    Public ReadOnly Property NetVesselProdRate As Double Implements ICraneLogsReport.NetVesselProdRate
        Get
            Return TotalMoves / NetVesselWorkingTime
        End Get
    End Property

    Public ReadOnly Property TotalGrossWorkingHours As Double Implements ICraneLogsReport.TotalGrossWorkingHours
        Get
            For Each crn As Crane In Crane
                If Crane IsNot Nothing Then TotalGrossWorkingHours += crn.GrossWorkingHours
            Next
            Return TotalGrossWorkingHours
        End Get
    End Property

    Public ReadOnly Property TotalNetWorkingHours As Double Implements ICraneLogsReport.TotalNetWorkingHours
        Get
            For Each crn As Crane In Crane
                If Crane IsNot Nothing Then TotalNetWorkingHours += crn.NetWorkingHours
            Next
            Return TotalGrossWorkingHours
        End Get
    End Property

    Public ReadOnly Property GrossCraneProductivity As Double Implements ICraneLogsReport.GrossCraneProductivity
        Get
            Return TotalMoves / TotalGrossWorkingHours
        End Get
    End Property

    Public ReadOnly Property NetCraneProductivity As Double Implements ICraneLogsReport.NetCraneProductivity
        Get
            Return TotalMoves / TotalNetWorkingHours
        End Get
    End Property


    Public Function CalculateInfo(strFunction As String, Inputs() As String) As Object Implements IReportswSave.CalculateInfo
        Throw New NotImplementedException()
    End Function

    Public Sub Format(ByRef crReport As ReportClass) Implements IReportswSave.Format
        Throw New NotImplementedException()
    End Sub

    Public Sub Preview(ByRef crReport As ReportClass, crViewer As CrystalReportViewer) Implements IReportswSave.Preview
        Throw New NotImplementedException()
    End Sub

    Public Sub Save() Implements IReportswSave.Save
        Dim refkeyCLR As Integer
        DateNow = Date.Now 'get date   

        OPConnection.Open()
        OPConnection.BeginTrans()
        Try
            refkeyCLR = SaveCraneLogsReport()
            SaveBerthDelays(refkeyCLR)
            SaveCranes(refkeyCLR)
            OPConnection.CommitTrans()
        Catch ex As Exception
            OPConnection.RollbackTrans()
        End Try

    End Sub

    Private Sub SaveCranes(refkeyCLR As Integer)
        Dim insertcommand As New ADODB.Command
        insertcommand.ActiveConnection = OPConnection

        For Each crn As Crane In Crane
            Dim refkeyCrane As Integer
            'save crane then get generated refkey
            insertcommand.CommandText = $"
INSERT INTO [opreports].[dbo].[crane]
           ([che_qc]
           ,[registry]
           ,[clr_refkey]
           ,[first_move]
           ,[last_move]
           ,[moves])
     VALUES
           ('{crn.CraneName}'
           ,'{CLRVessel.Registry}'
           ,{refkeyCLR}
           ,'{crn.FirstMove}'
           ,'{crn.LastMove}'
           ,{crn.Moves.TotalMoves}

    Select Scope_Identity() as NewID
"
            refkeyCrane = insertcommand.Execute.Fields(0).Value

            'use refkey to save the crane's container, gearbox, hatchcover moves, and, delays

            SaveContainerMoves(crn, refkeyCrane)
            SaveGearboxMoves(crn, refkeyCrane)
            SaveHatchcoverMoves(crn, refkeyCrane)
            SaveDelays(crn, refkeyCrane)

        Next
    End Sub

    Private Sub SaveDelays(crn As Crane, refkeyCrane As Integer)
        SaveDeductableDelays(crn, refkeyCrane)
        SaveBreaktimeDelays(crn, refkeyCrane)
        SaveNondeductableDelays(crn, refkeyCrane)
    End Sub

    Private Sub SaveNondeductableDelays(crn As Crane, refkeyCrane As Integer)
        Dim insertcommand As New ADODB.Command
        insertcommand.ActiveConnection = OPConnection
        For Each delay As DataRow In crn.Delays.Nondeductable.Rows
            insertcommand.CommandText = $"
INSERT INTO [opreports].[dbo].[crane_delays]
           ([crane_refkey]
           ,[che_qc]
           ,[delay_kind]
           ,[description]
           ,[delaystart]
           ,[delayend])
     VALUES
           ({refkeyCrane}
           ,{crn.CraneName}
           ,'NONDE'
           ,'{delay("description").ToString}'
           ,'{delay("delaystart").ToString}'
           ,'{delay("delayend").ToString}'
"
            insertcommand.Execute()
        Next
    End Sub

    Private Sub SaveBreaktimeDelays(crn As Crane, refkeyCrane As Integer)
        Dim insertcommand As New ADODB.Command
        insertcommand.ActiveConnection = OPConnection
        For Each delay As DataRow In crn.Delays.Break.Rows
            insertcommand.CommandText = $"
INSERT INTO [opreports].[dbo].[crane_delays]
           ([crane_refkey]
           ,[che_qc]
           ,[delay_kind]
           ,[description]
           ,[delaystart]
           ,[delayend])
     VALUES
           ({refkeyCrane}
           ,{crn.CraneName}
           ,'BREAK'
           ,'{delay("description").ToString}'
           ,'{delay("delaystart").ToString}'
           ,'{delay("delayend").ToString}'
"
            insertcommand.Execute()
        Next
    End Sub

    Private Sub SaveDeductableDelays(crn As Crane, refkeyCrane As Integer)
        Dim insertcommand As New ADODB.Command
        insertcommand.ActiveConnection = OPConnection
        For Each delay As DataRow In crn.Delays.Deductable.Rows
            insertcommand.CommandText = $"
INSERT INTO [opreports].[dbo].[crane_delays]
           ([crane_refkey]
           ,[che_qc]
           ,[delay_kind]
           ,[description]
           ,[delaystart]
           ,[delayend])
     VALUES
           ({refkeyCrane}
           ,{crn.CraneName}
           ,'DEDUC'
           ,'{delay("description").ToString}'
           ,'{delay("delaystart").ToString}'
           ,'{delay("delayend").ToString}'
"
            insertcommand.Execute()
        Next
    End Sub

    Private Sub SaveHatchcoverMoves(crn As Crane, refkeyCrane As Integer)
        Dim insertcommand As New ADODB.Command
        insertcommand.ActiveConnection = OPConnection
        For Each mve As DataRow In crn.Moves.Hatchcover.Rows
            insertcommand.CommandText = $"
INSERT INTO [opreports].[dbo].[crane_hatchcovers]
           ([crane_refkey]
           ,[che_qc]
           ,[actual_ib]
           ,[actual_ob]
           ,[baynum]
           ,[20]
           ,[40])
     VALUES
           ({refkeyCrane}
           ,{crn.CraneName}
           ,{mve("actual_ib").ToString}
           ,{mve("actual_ob").ToString}
           ,{mve("baynum").ToString}
           ,{mve("cntsze20").ToString}
           ,{mve("cntsze40").ToString}
"
            insertcommand.Execute()
        Next
    End Sub

    Private Sub SaveGearboxMoves(crn As Crane, refkeyCrane As Integer)
        Dim insertcommand As New ADODB.Command
        insertcommand.ActiveConnection = OPConnection
        For Each mve As DataRow In crn.Moves.Gearbox.Rows
            insertcommand.CommandText = $"
INSERT INTO [opreports].[dbo].[crane_gearboxes]
           ([crane_refkey]
           ,[che_qc]
           ,[actual_ib]
           ,[actual_ob]
           ,[baynum]
           ,[20]
           ,[40])
     VALUES
           ({refkeyCrane}
           ,{crn.CraneName}
           ,{mve("actual_ib").ToString}
           ,{mve("actual_ob").ToString}
           ,{mve("baynum").ToString}
           ,{mve("cntsze20").ToString}
           ,{mve("cntsze40").ToString}
"
            insertcommand.Execute()
        Next
    End Sub

    Private Sub SaveContainerMoves(crn As Crane, refkeyCrane As Integer)
        Dim insertcommand As New ADODB.Command
        insertcommand.ActiveConnection = OPConnection
        For Each mve As DataRow In crn.Moves.Container.Rows
            insertcommand.CommandText = $"
INSERT INTO [opreports].[dbo].[crane_containers]
           ([crane_refkey]
           ,[che_qc]
           ,[move_kind]
           ,[actual_ib]
           ,[actual_ob]
           ,[freight_kind]
           ,[20]
           ,[40]
           ,[45])
     VALUES
           ({refkeyCrane}
           ,'{crn.CraneName}'
           ,'{mve("move_kind").ToString}'
           ,'{mve("actual_ib").ToString}'
           ,'{mve("actual_ob").ToString}'
           ,'{mve("freight_kind").ToString}'
           ,{mve("cntsze20").ToString}
           ,{mve("cntsze40").ToString}
           ,{mve("cntsze45").ToString}
"
            insertcommand.Execute()
        Next
    End Sub

    Private Sub SaveBerthDelays(refkeyCLR As Integer)
        Dim insertcommand As New ADODB.Command
        insertcommand.ActiveConnection = OPConnection
        For Each bhdrow As DataRow In CraneLogsData.BerthingHourDelays.Rows
            insertcommand.CommandText = $"
INSERT INTO [opreports].[dbo].[clr_berthdelays]
           ([registry]
           ,[clr_refkey]
           ,[berthdelay]
           ,[delaystart]
           ,[delayend])
     VALUES
           ({CLRVessel.Registry}
           ,{refkeyCLR}       
           ,'{bhdrow("berthdelay").ToString}'
           ,'{bhdrow("delaystart").ToString}'
           ,'{bhdrow("delayend").ToString}')
"
            insertcommand.Execute()
        Next
    End Sub

    Private Function SaveCraneLogsReport() As Integer
        Dim insertcommand As New ADODB.Command
        insertcommand.ActiveConnection = OPConnection
        insertcommand.CommandText = $"
INSERT INTO [opreports].[dbo].[reports_clr]
           ([registry]
           ,[vslname]
           ,[owner]
           ,[last_port]
           ,[next_port]
           ,[ata]
           ,[atd]
           ,[first_move]
           ,[last_move]
           ,[moves]
           ,[created]
           ,[userid])
     VALUES
           ('{CLRVessel.Registry}'
           ,'{CLRVessel.Name}'
           ,'{CLRVessel.Owner}'
           ,'{Me.LastPort}'
           ,'{Me.NextPort}'
           ,'{CLRVessel.ATA}'
           ,'{CLRVessel.ATD}'
           ,'{Me.FirstMove}'
           ,'{Me.LastMove}'
           ,{Me.TotalMoves}
           ,'{UserName}'
           ,'{DateNow}'
           )

      Select Scope_Identity() as NewID
"
        Return insertcommand.Execute.Fields(0).Value 'Jumper to catch value of insert command after execution
    End Function

    Public Sub RetrieveData() Implements IReportswSave.RetrieveData 'different implementation; used only if clr is existing

        Dim cranelogsRefkey As Integer = GetRefkey()
        GetBerthDelays(cranelogsRefkey)
        GetCranes(cranelogsRefkey)
    End Sub

    Private Function GetRefkey() As Integer
        Dim cranelogRetriever As New ADODB.Command
        cranelogRetriever.ActiveConnection = OPConnection

        'GET Refkey
        cranelogRetriever.CommandText = $"
SELECT [refkey]
FROM [opreports].[dbo].[reports_clr]
Where registry = '{CLRVessel.Registry}'"

        Return cranelogRetriever.Execute.Fields("refkey").Value.ToString 'tostring just to be safe
    End Function
    Private Sub GetBerthDelays(Refkey As Integer)
        Dim berthdelayRetriever As New ADODB.Command
        berthdelayRetriever.ActiveConnection = OPConnection
        berthdelayRetriever.CommandText = $"SELECt [berthdelay]
      ,[delaystart]
      ,[delayend]
  FROM [opreports].[dbo].[clr_berthdelays]
	
	WHERE [clr_refkey] = {Refkey}
"
        Dim dataAdapter As New OleDb.OleDbDataAdapter
        dataAdapter.Fill(CraneLogsData.BerthingHourDelays, berthdelayRetriever.Execute) 'shortcut to fill instead of copying the returned recordset of execute

    End Sub
    Private Sub GetCranes(Refkey As Integer)
        Dim craneRetriever As New ADODB.Command With {
            .ActiveConnection = OPConnection,
            .CommandText = $"
SELECT  [refkey]
      ,[che_qc]
      ,[first_move]
      ,[last_move]
      ,[moves]
  FROM [opreports].[dbo].[crane]
	
	WHERE [clr_refkey] = {Refkey}
"
        }
        With craneRetriever.Execute
            Try
                .MoveFirst()
            Catch
            Finally
                While Not (.EOF Or .BOF)
                    Dim temporaryCrane As New Crane(.Fields("qc_shortname").Value, CLRVessel.Registry, N4Connection)
                    temporaryCrane.Moves.Container.Clear() 'removes preloaded data

                    temporaryCrane.FirstMove = .Fields("first_move").Value
                    temporaryCrane.LastMove = .Fields("last_move").Value

                    Dim craneRefkey = .Fields("refkey").Value
                    Dim informationFiller As New OleDb.OleDbDataAdapter
                    With informationFiller
                        .Fill(temporaryCrane.Moves.Container, GetContainerMoves(craneRefkey))
                        .Fill(temporaryCrane.Moves.Gearbox, GetGearboxMoves(craneRefkey))
                        .Fill(temporaryCrane.Moves.Hatchcover, GetHatchcoverMoves(craneRefkey))

                        PopulateDelays(temporaryCrane, GetCraneDelays(craneRefkey))
                    End With
                End While
            End Try
        End With
    End Sub

    Private Sub PopulateDelays(ByRef temporaryCrane As Crane, recordset As Recordset)
        With recordset
            Try
                .MoveFirst()
            Finally
                While Not (.EOF Or .BOF)
                    Dim tableName As String = .Fields("delay_kind").Value
                    Dim description As String = .Fields("description").Value
                    Dim delayFrom As Date = .Fields("delayfrom").Value
                    Dim delayTo As Date = .Fields("delayto").Value
                    Dim span As TimeSpan = delayTo.Subtract(delayFrom)

                    temporaryCrane.Delays.Tables.Item(tableName).Rows.Add({description,
                                                                          delayFrom,
                                                                          delayTo,
                                                                          span.TotalHours})
                End While
            End Try
        End With
    End Sub

    Private Function GetCraneDelays(craneRefkey As Object) As ADODB.Recordset
        Dim craneDelays As New ADODB.Command
        craneDelays.ActiveConnection = OPConnection
        craneDelays.CommandText = $"
SELECT [delay_kind]
      ,[description]
      ,[delaystart]
      ,[delayend]
  FROM [opreports].[dbo].[crane_delays]
    WHERE [crane_refkey] = {craneRefkey}
"
        Return craneDelays.Execute

    End Function

    Private Function GetHatchcoverMoves(craneRefkey As Object) As Object
        Dim hatchCoverMoves As New ADODB.Command
        With hatchCoverMoves
            .ActiveConnection = OPConnection
            .CommandText = $"
SELECT [move_kind]
      ,[baynum]
      ,[20]
      ,[40]
  FROM [opreports].[dbo].[crane_hatchcovers]
    WHERE crane_refkey = {craneRefkey}
"
            Return hatchCoverMoves.Execute
        End With
    End Function

    Private Function GetGearboxMoves(craneRefkey As Object) As ADODB.Recordset
        Dim gearboxMoves As New ADODB.Command
        With gearboxMoves
            .ActiveConnection = OPConnection
            .CommandText = $"
SELECT [move_kind]
      ,[baynum]
      ,[20]
      ,[40]
  FROM [opreports].[dbo].[crane_gearboxes]
    WHERE crane_refkey = {craneRefkey}
"
            Return gearboxMoves.Execute
        End With
    End Function

    Private Function GetContainerMoves(craneRefkey As Object) As ADODB.Recordset
        Dim containerMoves As New ADODB.Command
        containerMoves.ActiveConnection = OPConnection
        containerMoves.CommandText = $"
SELECT [move_kind]
      ,[ctrtyp]
      ,[freigh]
      ,[20]
      ,[40]
      ,[45]
  FROM [opreports].[dbo].[crane_containers]
	WHERE [crane_refkey] = {craneRefkey}
"
        Return containerMoves.Execute
    End Function

    Public Sub IntializeCrane(GantryName As String) Implements ICraneLogsReport.IntializeCrane
        Dim number As Integer = GantryName.Substring(GantryName.Length - 1, 1)
        Crane.Add(New Crane(GantryName, CLRVessel.Registry, N4Connection))
    End Sub

    Public Function Exists() As Boolean Implements IReportswSave.Exists ' no need register parameter since this can only be used when clr class is instantiated
        Dim craneLogsFinder As New ADODB.Command
        craneLogsFinder.ActiveConnection = OPConnection
        craneLogsFinder.CommandText = $"Select refkey from reports_clr where registry = '{CLRVessel.Registry}'" 'shortcut to registry since they will only point to the same thing  

        Dim craneLogs As New ADODB.Recordset
        craneLogs = craneLogsFinder.Execute()

        With craneLogs
            Return Not (.BOF And .EOF)
        End With

    End Function

End Class
