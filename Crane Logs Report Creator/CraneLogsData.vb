﻿Partial Class CraneLogsData
    Partial Public Class BerthingHourDelaysDataTable
        Public Function Totalhours() As Double
            Dim hours As Double = 0
            For Each row In Me.Rows
                hours += row("delayhours").ToString
            Next
            Return hours
        End Function
    End Class

    Partial Public Class CummulativeVolumeDataTable
        Private Sub CummulativeVolumeDataTable_CummulativeVolumeRowChanging(sender As Object, e As CummulativeVolumeRowChangeEvent) Handles Me.CummulativeVolumeRowChanging

        End Sub

    End Class

    Partial Public Class CraneProductivityDataTable
        Private Sub CraneProductivityDataTable_ColumnChanging(sender As Object, e As DataColumnChangeEventArgs) Handles Me.ColumnChanging


        End Sub

    End Class
End Class
