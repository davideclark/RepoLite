﻿Imports System.Data.SqlClient
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Xml
Imports RepoLite.VB.Tests.MODELNAMESPACE.Base

Namespace REPOSITORYNAMESPACE.Base
    Public Interface IBaseRepository (Of T)
        Function GetAll() As IEnumerable(Of T)
        Function Create(item As T) As Boolean
        Function BulkCreate(items As List(Of T)) As Boolean
        Function BulkCreate(ParamArray items() As T) As Boolean

        Function Where(col As String, comparison As Comparison, val As Object) As Where(Of T)

        Function Where(col As String, comparison As Comparison, val As Object, valueType As Type) As Where(Of T)

        Function Where(query As String) As IEnumerable(Of T)
    End Interface

    Public Interface IPkRepository (Of T)
        Inherits IBaseRepository(Of T)
        Function Update(item As T) As Boolean
        Function Delete(item As T) As Boolean
        Function Delete(items As IEnumerable(Of T)) As Boolean
        Function Merge(items As List(Of T)) As Boolean
    End Interface

#Region "Enums"

    Friend Enum ClauseType
        [Initial]
        [And]
        [Or]
    End Enum

    Public Enum FindComparison
        [Equals]
        [NotEquals]
        [Like]
        [NotLike]
        [GreaterThan]
        [GreaterThanOrEquals]
        [LessThan]
        [LessThanOrEquals]
    End Enum

    Public Enum Comparison
        [Equals]
        [NotEquals]
        [Like]
        [NotLike]
        [GreaterThan]
        [GreaterThanOrEquals]
        [LessThan]
        [LessThanOrEquals]
        [In]
        [NotIn]
        [IsNull]
        [IsNotNull]
    End Enum

#End Region

    Public Class DeleteColumn
        Public Property ColumnName As String
        Public Property SqlDbType As SqlDbType
        Public Property Data As Object

        Public Sub New(columnName As String, data As Object, sqlDbType As SqlDbType)
            Me.ColumnName = columnName
            Me.Data = data
            Me.SqlDbType = sqlDbType
        End Sub
    End Class

    Public Class ColumnDefinition
        Public Property ColumnName As String
        Public Property ValueType As Type
        Public Property SqlDataTypeText As String
        Public Property SqlDbType As SqlDbType
        Public Property Identity As Boolean
        Public Property PrimaryKey As Boolean
        Public Property Nullable As Boolean

        Public Sub New(columnName As String, valueType As Type, sqlDataTypeText As String,
                       sqlDbType As SqlDbType)
            Me.New(columnName, valueType, sqlDataTypeText, sqlDbType, False, False, False)
        End Sub

        Public Sub New(columnName As String, valueType As Type, sqlDataTypeText As String,
                       sqlDbType As SqlDbType, nullable As Boolean)
            Me.New(columnName, valueType, sqlDataTypeText, sqlDbType, nullable, False, False)
        End Sub

        Public Sub New(columnName As String, valueType As Type, sqlDataTypeText As String,
                       sqlDbType As SqlDbType, nullable As Boolean, primaryKey As Boolean)
            Me.New(columnName, valueType, sqlDataTypeText, sqlDbType, nullable, primaryKey, False)
        End Sub

        Public Sub New(columnName As String, valueType As Type, sqlDataTypeText As String,
                       sqlDbType As SqlDbType, nullable As Boolean, primaryKey As Boolean,
                       identity As Boolean)
            Me.ColumnName = columnName
            Me.ValueType = valueType
            Me.SqlDataTypeText = sqlDataTypeText
            Me.SqlDbType = sqlDbType
            Me.Nullable = nullable
            Me.PrimaryKey = primaryKey
            Me.Identity = identity
        End Sub
    End Class

    Public Class QueryItem
        Public Property DbColumnName As String
        Public Property Value As Object
        Public Property DataType As Type

        Public Sub New(dbColName As String, value As Object)
            Me.New(dbColName, value, value.[GetType]())
        End Sub

        Public Sub New(dbColumnName As String, value As Object, dataType As Type)
            Me.DbColumnName = dbColumnName
            Me.Value = value
            Me.DataType = dataType
        End Sub
    End Class

    Public Class ValidationException
        Inherits Exception

        Public Property ValidationErrors As List(Of ValidationError)

        Public Sub New(validationErrors As List(Of ValidationError))
            Me.ValidationErrors = validationErrors
        End Sub
    End Class

    Public Class Where (Of T)
        Private ReadOnly _query As StringBuilder = New StringBuilder()
        Private ReadOnly _repository As BaseRepository(Of T)
        Private _activeGroups As Integer

        Public Sub New(baseRepository As BaseRepository(Of T), col As String, comparison As Comparison,
                       val As Object)
            Me.New(baseRepository, col, comparison, val, val.[GetType]())
        End Sub

        Public Sub New(baseRepository As BaseRepository(Of T), col As String, comparison As Comparison,
                       val As Object, valueType As Type)
            _repository = baseRepository
            _query.Append(MakeClause(col, comparison, val, ClauseType.Initial, valueType))
        End Sub

        Private Function MakeClause(col As String, comparison As Comparison, clauseType As ClauseType) As String
            Return MakeClause(col, comparison, Nothing, clauseType, Nothing)
        End Function

        Private Function MakeClause(col As String, comparison As Comparison, val As Object,
                                    clauseType As ClauseType, valueType As Type) As String
            Dim query = New StringBuilder()

            Select Case comparison
                Case Comparison.[In], Comparison.NotIn
                    If TryCast(val, IEnumerable) IsNot Nothing And Not If(TryCast(val, Object()), TryCast(val, IEnumerable).Cast (Of Object)().ToArray()).Any() Then
                        query.Append("1=0")
                        Return query.ToString()
                    End If
            End Select

            Dim floatVal As Single
            If _
                {Comparison.GreaterThan, Comparison.GreaterThanOrEquals, Comparison.LessThan,
                 Comparison.LessThanOrEquals}.Contains(comparison) AndAlso Not Single.TryParse(val.ToString(), floatVal) _
                Then
                Throw New Exception("Numeric comparison used on a non numeric value.")
            End If

            Select Case clauseType
                Case ClauseType.Initial
                    query.Append(
                        If(valueType = GetType(XmlDocument), "CONVERT(NVARCHAR(MAX), [" & col & "])", "[" & col & "]"))
                Case ClauseType.[And]
                    query.Append(
                        If _
                                    (valueType = GetType(XmlDocument), " AND CONVERT(NVARCHAR(MAX), [" & col & "])",
                                     " AND [" & col & "]"))
                Case ClauseType.[Or]
                    query.Append(
                        If _
                                    (valueType = GetType(XmlDocument), " OR CONVERT(NVARCHAR(MAX), [" & col & "])",
                                     " OR [" & col & "]"))
            End Select

            query.Append(GetComparison(comparison))

            If comparison <> Comparison.IsNull AndAlso comparison <> Comparison.IsNotNull Then
                Dim typeVal = GetTypeVal(col, val)
                If comparison = Comparison.[Like] OrElse comparison = Comparison.NotLike Then _
                    typeVal = typeVal.TrimStart("'"c).TrimEnd("'"c)
                query.Append(typeVal)
            End If

            Select Case comparison
                Case Comparison.[In], Comparison.NotIn
                    query.Append(")")
                Case Comparison.[Like], Comparison.NotLike
                    query.Append("%'")
            End Select

            Return query.ToString()
        End Function

        Private Shared Function GetComparison(comparison As Comparison) As String
            Select Case comparison
                Case Comparison.Equals
                    Return " = "
                Case Comparison.NotEquals
                    Return " <> "
                Case Comparison.[Like]
                    Return " LIKE '%"
                Case Comparison.NotLike
                    Return " NOT LIKE '%"
                Case Comparison.GreaterThan
                    Return " > "
                Case Comparison.GreaterThanOrEquals
                    Return " >= "
                Case Comparison.LessThan
                    Return " < "
                Case Comparison.LessThanOrEquals
                    Return " <= "
                Case Comparison.[In]
                    Return " IN ("
                Case Comparison.NotIn
                    Return " NOT IN ("
                Case Comparison.IsNull
                    Return " IS NULL"
                Case Comparison.IsNotNull
                    Return " IS NOT NULL"
                Case Else
                    Throw New NotSupportedException("???")
            End Select
        End Function

        Private Function GetTypeVal(col As String, val As Object) As String
            Dim typeName = If(TypeOf val Is IList, "List", val.[GetType]().Name)

            Select Case typeName
                Case "Boolean"
                    If val Then Return "1"
                    Return "0"
                Case "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "Decimal", "Double"
                    Return val.ToString()
                Case "DateTime", "Char", "String", "Guid", "TimeSpan", "DateTimeOffset"
                    Return "'" & val & "'"
                Case "List"
                    Dim result = ""
                    Dim enumerable =
                            If(TryCast(val, Object()), TryCast(val, IEnumerable).Cast (Of Object)().ToArray())
                    Const batchSize = 2000
                    Dim batches = Math.Ceiling(CDec(enumerable.Length)/batchSize)

                    For i = 0 To batches - 1
                        result =
                            enumerable.Skip(i*batchSize).Take(batchSize).Aggregate(result,
                                                                                   Function(current, o) _
                                                                                      current & GetTypeVal(col, o) &
                                                                                      ", ").TrimEnd(" "c).TrimEnd(","c)
                        If batches > i + 1 Then result += ") OR [" & col & "] IN ("
                    Next

                    Return result
                Case Else
                    Throw New NotSupportedException("Not supported yet")
            End Select
        End Function

        Public Function Results() As IEnumerable(Of T)
            If _activeGroups > 0 Then Throw New Exception("Please close all Query Groups before calling Results()")
            Return _repository.Where(_query.ToString())
        End Function

        Public Function [And](col As String, comparison As Comparison) As Where(Of T)
            If comparison <> Comparison.IsNull AndAlso comparison <> Comparison.IsNotNull Then _
                Throw New Exception(
                    "And(" & col & ", " & comparison &
                    ") can only be called with Comparison.IsNull or Comparison.IsNotNull")
            _query.Append(MakeClause(col, comparison, ClauseType.[And]))
            Return Me
        End Function

        Public Function [And](col As String, comparison As Comparison, val As Object) As Where(Of T)
            Return [And](col, comparison, val, val.[GetType]())
        End Function

        Public Function [And](col As String, comparison As Comparison, val As Object,
                              valueType As Type) As Where(Of T)
            _query.Append(MakeClause(col, comparison, val, ClauseType.[And], valueType))
            Return Me
        End Function

        Public Function [Or](col As String, comparison As Comparison) As Where(Of T)
            If comparison <> Comparison.IsNull AndAlso comparison <> Comparison.IsNotNull Then _
                Throw New Exception(
                    "Or(" & col & ", " & comparison &
                    ") can only be called with Comparison.IsNull or Comparison.IsNotNull")
            _query.Append(MakeClause(col, comparison, ClauseType.[Or]))
            Return Me
        End Function

        Public Function [Or](col As String, comparison As Comparison, val As Object) As Where(Of T)
            Return [Or](col, comparison, val, val.[GetType]())
        End Function

        Public Function [Or](col As String, comparison As Comparison, val As Object,
                             valueType As Type) As Where(Of T)
            _query.Append(MakeClause(col, comparison, val, ClauseType.[Or], valueType))
            Return Me
        End Function

        Public Function AndBeginGroup(col As String, comparison As Comparison) As Where(Of T)
            If comparison <> Comparison.IsNull AndAlso comparison <> Comparison.IsNotNull Then _
                Throw New Exception(
                    "AndBeginGroup(" & col & ", " & comparison &
                    ") can only be called with Comparison.IsNull or Comparison.IsNotNull")
            _activeGroups += 1
            _query.Append(" AND (" & MakeClause(col, comparison, ClauseType.Initial))
            Return Me
        End Function

        Public Function AndBeginGroup(col As String, comparison As Comparison, val As Object) _
            As Where(Of T)
            Return AndBeginGroup(col, comparison, val, val.[GetType]())
        End Function

        Public Function AndBeginGroup(col As String, comparison As Comparison, val As Object,
                                      valueType As Type) As Where(Of T)
            _activeGroups += 1
            _query.Append(" AND (" & MakeClause(col, comparison, val, ClauseType.Initial, valueType))
            Return Me
        End Function

        Public Function OrBeginGroup(col As String, comparison As Comparison) As Where(Of T)
            If comparison <> Comparison.IsNull AndAlso comparison <> Comparison.IsNotNull Then _
                Throw _
                    New Exception(
                        "OrBeginGroup(" & col & ", " & comparison &
                        ") can only be called with Comparison.IsNull or Comparison.IsNotNull")
            _activeGroups += 1
            _query.Append(" OR (" & MakeClause(col, comparison, ClauseType.Initial))
            Return Me
        End Function

        Public Function OrBeginGroup(col As String, comparison As Comparison, val As Object) _
            As Where(Of T)
            Return OrBeginGroup(col, comparison, val, val.[GetType]())
        End Function

        Public Function OrBeginGroup(col As String, comparison As Comparison, val As Object,
                                     valueType As Type) As Where(Of T)
            _activeGroups += 1
            _query.Append(" OR (" & MakeClause(col, comparison, val, ClauseType.Initial, valueType))
            Return Me
        End Function

        Public Function EndGroup() As Where(Of T)
            _activeGroups -= 1
            _query.Append(")")
            Return Me
        End Function

        Public Function QueryString() As String
            Return _repository.WhereQuery() & " WHERE " & _query.ToString()
        End Function
    End Class

    Public MustInherit Class BaseRepository (Of T)
        Implements IBaseRepository(Of T)

        Protected Logger As Action(Of Exception)
        Protected ConnectionString As String
        Private ReadOnly _schema As String
        Private ReadOnly _tableName As String
        Public Property Columns As List(Of ColumnDefinition)

        Protected Sub New(connectionString As String, logMethod As Action(Of Exception),
                          schema As String, table As String, columnCount As Integer)
            Columns = New List(Of ColumnDefinition)()
            _schema = schema
            _tableName = table
            Me.ConnectionString = connectionString
            Logger = If(logMethod, (Function(exception)
            End Function))
            Dim sql =
                    $"SELECT COUNT(*)
                            FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME = '{ _
                    table}' AND TABLE_SCHEMA = '{schema}'"

            Using cn = New SqlConnection(connectionString)

                Using cmd = CreateCommand(cn, sql)

                    Try
                        cn.Open()
                        Dim count = CInt(cmd.ExecuteScalar())
                        If count <> columnCount Then _
                            Throw New Exception(
                                "Repository Definition does not match Database. Please re-run the code generator to get a new repository")
                    Finally
                        cn.Close()
                    End Try
                End Using
            End Using
        End Sub

        Public Function GetAll() As IEnumerable(Of T) Implements IBaseRepository(Of T).GetAll
            Return Where("1=1")
        End Function

        Public MustOverride Function Create(item As T) As Boolean Implements IBaseRepository(Of T).Create

        Public MustOverride Function BulkCreate(items As List(Of T)) As Boolean _
            Implements IBaseRepository(Of T).BulkCreate

        Public MustOverride Function BulkCreate(ParamArray items As T()) As Boolean _
            Implements IBaseRepository(Of T).BulkCreate

        Protected MustOverride Function ToItem(row As DataRow) As T

        Protected Function CreateCommand(cn As SqlConnection, command As String) As SqlCommand
            Dim cmd = New SqlCommand With {
                    .Connection = cn,
                    .CommandType = CommandType.Text,
                    .CommandText = command
                    }
            Return cmd
        End Function

        Protected Friend Function WhereQuery() As String
            Dim sb = New StringBuilder()
            sb.AppendLine("SELECT ")

            For Each column In Columns
                sb.Append(column.ColumnName)
                If column IsNot Columns.Last() Then sb.Append(", ")
            Next

            sb.Append($" FROM [{_schema}].[{_tableName}]")
            Return sb.ToString()
        End Function

        Public Function Where(col As String, comparison As Comparison, val As Object) As Where(Of T) _
            Implements IBaseRepository(Of T).Where
            Return Where(col, comparison, val, val.[GetType]())
        End Function

        Public Function Where(col As String, comparison As Comparison, val As Object,
                              valueType As Type) As Where(Of T) Implements IBaseRepository(Of T).Where
            Return New Where(Of T)(Me, col, comparison, val, valueType)
        End Function

        Public Function Where(query As String) As IEnumerable(Of T) Implements IBaseRepository(Of T).Where
            If HasInjection(query) Then Throw New Exception("Sql Injection attempted. Aborted")

            Using cn = New SqlConnection(ConnectionString)

                Using cmd = CreateCommand(cn, $"{WhereQuery()} WHERE {query}")
                    If HasInjection(cmd.CommandText) Then Throw New Exception("Sql Injection attempted. Aborted")
                    cn.Open()
                    Dim dt = ToDataTable(cmd)
                    If dt Is Nothing Then Return New T() {}
                    Dim items = ToItems(dt)
                    cn.Close()
                    Return items
                End Using
            End Using
        End Function

        Protected Function HasInjection(query As String) As Boolean
            Dim isSqlInjection = False
            Dim sqlCheckList As String() = {"--", ";--", "/*", "*/"}
            Dim checkString = query.Replace("'", "''")

            For i = 0 To sqlCheckList.Length - 1
                If checkString.IndexOf(sqlCheckList(i), StringComparison.OrdinalIgnoreCase) < 0 Then Continue For
                isSqlInjection = True
                Exit For
            Next

            Return isSqlInjection
        End Function

        Protected Function BaseSearch(queries As List(Of QueryItem)) As IEnumerable(Of T)
            If Not queries.Any() Then Return New List(Of T)()
            Dim first = queries.First()
            Dim whereQuery = Where(first.DbColumnName, Comparison.Equals, first.Value, first.DataType)

            If queries.Count > 1 Then
                whereQuery = queries.Skip(1).Aggregate(whereQuery,
                                                       Function(current, query) _
                                                          current.[And](query.DbColumnName, Comparison.Equals,
                                                                        query.Value, first.DataType))
            End If

            Return whereQuery.Results()
        End Function

        Protected Function BaseCreate(ParamArray values As Object()) As Dictionary(Of String, Object)
            Dim returnIds = New Dictionary(Of String, Object)()

            Using cn = New SqlConnection(ConnectionString)
                Dim sb = New StringBuilder()
                Dim pkCols = Columns.Where(Function(x) x.PrimaryKey).ToList()

                If Columns.Any(Function(x) x.PrimaryKey) Then
                    sb.AppendLine("DECLARE @tempo TABLE (")

                    For Each pk In pkCols
                        sb.Append($"[{pk.ColumnName}]  {pk.SqlDataTypeText}")
                        sb.AppendLine(If(pk IsNot pkCols(pkCols.Count - 1), ",", String.Empty))
                    Next

                    sb.AppendLine(")")
                End If

                sb.AppendLine($"INSERT [{_schema}].[{_tableName}] (")
                Dim toCreate =
                        Columns.Where(Function(x) Not x.PrimaryKey OrElse x.PrimaryKey AndAlso Not x.Identity).ToList()

                For Each createColumn In toCreate
                    sb.Append($"[{createColumn.ColumnName}]")
                    sb.AppendLine(If(createColumn IsNot Columns.Last(), ",", ")"))
                Next

                If Columns.Any(Function(x) x.PrimaryKey) Then
                    sb.Append("OUTPUT ")

                    For Each pk In pkCols
                        sb.Append($"[Inserted].[{pk.ColumnName}] ")
                        sb.AppendLine(If(pk IsNot pkCols(pkCols.Count - 1), ",", String.Empty))
                    Next

                    sb.AppendLine("INTO @tempo ")
                End If

                sb.AppendLine("VALUES (")
                Dim valueCols =
                        Columns.Where(Function(x) Not x.PrimaryKey OrElse (x.PrimaryKey AndAlso Not x.Identity)).ToList()

                For Each createColumn In valueCols
                    sb.Append("@" & createColumn.ColumnName)
                    sb.AppendLine(If(createColumn IsNot valueCols.Last(), ",", ")"))
                Next

                If Columns.Any(Function(x) x.PrimaryKey) Then
                    sb.AppendLine("SELECT * FROM @tempo")
                End If

                Dim sql = sb.ToString()
                If HasInjection(sql) Then Throw New Exception("Sql Injection attempted. Aborted")

                Using cmd = CreateCommand(cn, sql)

                    For i = 0 To Columns.Count - 1
                        Dim createColumn = Columns(i)
                        If createColumn.PrimaryKey AndAlso (Not createColumn.PrimaryKey OrElse createColumn.Identity) _
                            Then Continue For
                        Dim parameter = cmd.Parameters.Add(createColumn.ColumnName, createColumn.SqlDbType)
                        parameter.Value =
                            If(values(i) IsNot Nothing,
                               If (values(i).GetType() = GetType(XmlDocument), CType(values(i), XmlDocument).InnerXml,
                                   values(i)), DBNull.Value)
                    Next

                    Dim dt As DataTable
                    Dim isSuccess = ToDataTable(cmd, cn, dt)
                    If Not isSuccess Then Return returnIds

                    If dt.Rows.Count > 0 Then

                        For i = 0 To dt.Columns.Count - 1
                            Dim dataColumn = dt.Columns(i)
                            returnIds.Add(dataColumn.ColumnName, dt.Rows(0)(i))
                        Next
                    End If
                End Using
            End Using

            Return returnIds
        End Function

        Protected Function BulkInsert(dt As DataTable, tableName As String) As Boolean
            Try
                Using cn = New SqlConnection(ConnectionString)
                    cn.Open()

                    Using bulkCopy =
                        New SqlBulkCopy(cn,
                                        SqlBulkCopyOptions.TableLock Or SqlBulkCopyOptions.FireTriggers Or
                                        SqlBulkCopyOptions.UseInternalTransaction, Nothing) With {
                                            .DestinationTableName = tableName
                                            }

                        For Each dataColumn As DataColumn In dt.Columns
                            bulkCopy.ColumnMappings.Add(dataColumn.ColumnName, dataColumn.ColumnName)
                        Next

                        bulkCopy.WriteToServer(dt)
                    End Using

                    cn.Close()
                    Return True
                End Using

            Catch ex As Exception
                Logger(ex)
                Return False
            End Try
        End Function

        Protected Function BulkInsert(dt As DataTable) As Boolean
            Return BulkInsert(dt, $"[{_schema}].[{_tableName}]")
        End Function

        Protected Function BaseUpdate(dirtyColumns As List(Of String), ParamArray values As Object()) As Boolean
            Dim isSuccess As Boolean
            Dim sb = New StringBuilder()
            sb.AppendLine($"UPDATE [{_schema}].[{_tableName}] SET")
            Dim nonPkCols = Columns.Where(Function(x) Not x.PrimaryKey).ToArray()

            For Each col In nonPkCols.Where(Function(x) dirtyColumns.Contains(x.ColumnName))
                sb.Append($"[{col.ColumnName}] = @{col.ColumnName}")
                sb.AppendLine(If(col IsNot nonPkCols.Last(Function(x) dirtyColumns.Contains(x.ColumnName)), ",", ""))
            Next

            sb.AppendLine("WHERE")
            Dim pkCols = Columns.Where(Function(x) x.PrimaryKey).ToArray()

            For Each pk In pkCols
                sb.AppendLine(
                    If _
                                 (pk Is pkCols.First(), $"[{pk.ColumnName}] = @{pk.ColumnName}",
                                  $"AND [{pk.ColumnName}] = @{pk.ColumnName}"))
            Next

            Dim sql = sb.ToString()
            If HasInjection(sql) Then Throw New Exception("Sql Injection attempted. Aborted")

            Using cn = New SqlConnection(ConnectionString)

                Using cmd = CreateCommand(cn, sql)

                    For i = 0 To Columns.Count - 1
                        Dim updateColumn = Columns(i)
                        If Not dirtyColumns.Contains(updateColumn.ColumnName) AndAlso Not updateColumn.PrimaryKey Then _
                            Continue For
                        Dim parameter = cmd.Parameters.Add(updateColumn.ColumnName, updateColumn.SqlDbType)

                        If updateColumn.PrimaryKey Then
                            parameter.Value = values(i)
                        Else
                            parameter.Value =
                                If _
                                    (dirtyColumns.Contains(updateColumn.ColumnName), If(values(i), DBNull.Value),
                                     values(i))
                        End If
                    Next

                    isSuccess = NoneQuery(cn, cmd)
                End Using
            End Using

            Return isSuccess
        End Function

        Protected Function BaseDelete(deleteColumn As DeleteColumn) As Boolean
            Dim isSuccess As Boolean

            Using cn = New SqlConnection(ConnectionString)
                Dim sb = New StringBuilder()
                sb.Append($"DELETE [{_schema}].[{_tableName}] WHERE ")
                sb.Append($"[{deleteColumn.ColumnName}] = @{deleteColumn.ColumnName}")
                Dim sql = sb.ToString()
                If HasInjection(sql) Then Throw New Exception("Sql Injection attempted. Aborted")

                Using cmd = CreateCommand(cn, sql)
                    Dim parameter = cmd.Parameters.Add(deleteColumn.ColumnName, deleteColumn.SqlDbType)
                    parameter.Value = deleteColumn.Data
                    isSuccess = NoneQuery(cn, cmd)
                End Using
            End Using

            Return isSuccess
        End Function

        Protected Function BaseDelete(columnName As String, dataValues As List(Of Object)) As Boolean
            Dim isSuccess As Boolean

            Using cn = New SqlConnection(ConnectionString)
                Dim sb = New StringBuilder()
                sb.Append($"DELETE [{_schema}].[{_tableName}] WHERE [{columnName}] IN (")

                For Each dataValue In dataValues
                    sb.Append(dataValue)
                    If dataValue <> dataValues.Last() Then sb.Append(", ")
                Next

                sb.Append(")")
                Dim sql = sb.ToString()
                If HasInjection(sql) Then Throw New Exception("Sql Injection attempted. Aborted")

                Using cmd = CreateCommand(cn, sql)
                    isSuccess = NoneQuery(cn, cmd)
                End Using
            End Using

            Return isSuccess
        End Function

        Protected Function BaseMerge(mergeData As List(Of Object())) As Boolean
            Dim tempTableName = "staging" & DateTime.Now.Ticks

            Try
                Dim dt = New DataTable()

                For Each mergeColumn In Columns
                    dt.Columns.Add(mergeColumn.ColumnName, mergeColumn.ValueType)
                    If Not mergeColumn.PrimaryKey Then _
                        dt.Columns.Add(mergeColumn.ColumnName & "Changed", GetType(Boolean))
                Next

                For Each m In mergeData
                    dt.Rows.Add(m)
                Next

                CreateStagingTable(tempTableName)
                BulkInsert(dt, tempTableName)

                Using cn = New SqlConnection(ConnectionString)
                    Dim mergeSql = New StringBuilder()
                    mergeSql.AppendLine($"MERGE INTO [{_schema}].[{_tableName}] AS [Target]")
                    mergeSql.AppendLine($"USING {tempTableName} AS Source")
                    mergeSql.AppendLine("ON")
                    Dim pks = Columns.Where(Function(x) x.PrimaryKey).ToArray()

                    For Each pk In pks
                        If pk IsNot pks.First() Then mergeSql.Append("AND ")
                        mergeSql.AppendLine($"[Target].[{pk.ColumnName}] = [Source].[{pk.ColumnName}]")
                    Next

                    mergeSql.AppendLine("WHEN MATCHED THEN UPDATE SET")
                    Dim nonpks = Columns.Where(Function(x) Not x.PrimaryKey).ToArray()

                    For Each mergeColumn In nonpks
                        mergeSql.Append(
                            $"[Target].[{mergeColumn.ColumnName}] = CASE WHEN [Source].[{mergeColumn.ColumnName _
                                           }Changed] = 1 THEN [Source].[{mergeColumn.ColumnName}] ELSE [Target].[{ _
                                           mergeColumn.ColumnName}] END")
                        mergeSql.AppendLine(If(mergeColumn IsNot nonpks.Last(), ",", Environment.NewLine))
                    Next

                    mergeSql.AppendLine("WHEN NOT MATCHED THEN INSERT (")
                    mergeSql.AppendLine(
                        String.Join(",",
                                    Columns.Where(Function(x) Not x.Identity).[Select](Function(x) $"[{x.ColumnName}]").
                                       ToArray()) & ")")
                    mergeSql.AppendLine("VALUES (")
                    mergeSql.AppendLine(
                        String.Join(",",
                                    Columns.Where(Function(x) Not x.Identity).[Select](
                                        Function(x) $"[Source].[{x.ColumnName}]").ToArray()) & ");")
                    mergeSql.AppendLine($"DROP TABLE {tempTableName}")
                    Dim sql = mergeSql.ToString()
                    If HasInjection(sql) Then Throw New Exception("Sql Injection attempted. Aborted")
                    Dim cmd = New SqlCommand With {
                            .Connection = cn,
                            .CommandType = CommandType.Text,
                            .CommandText = sql
                            }
                    
With cmd
    .Connection = cn
    .CommandType = CommandType.Text
    .CommandText = sql
End With
                    cn.Open()
                    cmd.ExecuteNonQuery()
                    cmd.Dispose()
                    Dim dropCmd = New SqlCommand With {
                            .Connection = cn,
                            .CommandType = CommandType.StoredProcedure,
                            .CommandText = "dbo.DropTmpTable"
                            }
                    dropCmd.Parameters.AddWithValue("table", tempTableName)
                    dropCmd.ExecuteNonQuery()
                    dropCmd.Dispose()
                    cn.Close()
                    Return True
                End Using

            Catch ex As Exception
                Logger(ex)

                Using cn = New SqlConnection(ConnectionString)
                    Dim cmd = New SqlCommand With {
                            .Connection = cn,
                            .CommandType = CommandType.Text,
                            .CommandText = $"DROP TABLE {tempTableName}"
                            }

                    Try
                        cmd.ExecuteNonQuery()
                    Catch ex2 As Exception
                        Logger(ex2)
                    End Try
                End Using

                Return False
            End Try
        End Function

        Protected Iterator Function ToItems(table As DataTable) As IEnumerable(Of T)
            For Each row As DataRow In table.Rows
                Dim item = Nothing

                Try
                    item = ToItem(row)
                Catch ex As Exception
                    Logger(ex)
                End Try

                Yield item
            Next
        End Function

        Protected Function ToDataTable(cmd As SqlCommand, cn As SqlConnection, <Out> ByRef dt As DataTable) As Boolean
            Dim isSuccess = True
            If HasInjection(cmd.CommandText) Then Throw New Exception("Sql Injection attempted. Aborted")
            cn.Open()
            dt = ToDataTable(cmd)
            cn.Close()
            If dt Is Nothing OrElse dt.Rows.Count = 0 Then isSuccess = False
            Return isSuccess
        End Function

        Protected Function NoneQuery(cn As SqlConnection, cmd As SqlCommand) As Boolean
            If HasInjection(cmd.CommandText) Then Throw New Exception("Sql Injection attempted. Aborted")
            Dim isSuccess = True
            cn.Open()

            Try
                cmd.ExecuteNonQuery()
            Catch ex As Exception
                Logger(ex)
                isSuccess = False
            End Try

            cn.Close()
            Return isSuccess
        End Function

        Protected Function GetBoolean(row As DataRow, fieldName As String) As Boolean
            Return If(row.GetValue (Of Boolean)(fieldName), False)
        End Function

        Protected Function GetNullableBoolean(row As DataRow, fieldName As String) As Boolean?
            Return row.GetValue (Of Boolean)(fieldName)
        End Function

        Protected Function GetInt16(row As DataRow, fieldName As String) As Short
            Return If(row.GetValue (Of Short)(fieldName), Nothing)
        End Function

        Protected Function GetNullableInt16(row As DataRow, fieldName As String) As Short?
            Return row.GetValue (Of Short)(fieldName)
        End Function

        Protected Function GetInt32(row As DataRow, fieldName As String) As Integer
            Return If(row.GetValue (Of Integer)(fieldName), Nothing)
        End Function

        Protected Function GetNullableInt32(row As DataRow, fieldName As String) As Integer?
            Return row.GetValue (Of Integer)(fieldName)
        End Function

        Protected Function GetInt64(row As DataRow, fieldName As String) As Long
            Return If(row.GetValue (Of Long)(fieldName), Nothing)
        End Function

        Protected Function GetNullableInt64(row As DataRow, fieldName As String) As Long?
            Return row.GetValue (Of Long)(fieldName)
        End Function

        Protected Function GetDecimal(row As DataRow, fieldName As String) As Decimal
            Return If(row.GetValue (Of Decimal)(fieldName), Nothing)
        End Function

        Protected Function GetNullableDecimal(row As DataRow, fieldName As String) As Decimal?
            Return row.GetValue (Of Decimal)(fieldName)
        End Function

        Protected Function GetDouble(row As DataRow, fieldName As String) As Double
            Return If(row.GetValue (Of Double)(fieldName), Nothing)
        End Function

        Protected Function GetNullableDouble(row As DataRow, fieldName As String) As Double?
            Return row.GetValue (Of Double)(fieldName)
        End Function

        Protected Function GetDateTime(row As DataRow, fieldName As String) As DateTime
            Return If(row.GetValue (Of DateTime)(fieldName), Nothing)
        End Function

        Protected Function GetNullableDateTime(row As DataRow, fieldName As String) As DateTime?
            Return row.GetValue (Of DateTime)(fieldName)
        End Function

        Protected Function GetByte(row As DataRow, fieldName As String) As Byte
            Return CByte(row(fieldName))
        End Function

        Protected Function GetNullableByte(row As DataRow, fieldName As String) As Byte?
            Return CType(row(fieldName), Byte?)
        End Function

        Protected Function GetByteArray(row As DataRow, fieldName As String) As Byte()
            Return CType(row(fieldName), Byte())
        End Function

        Protected Function GetDateTimeOffset(row As DataRow, fieldName As String) As DateTimeOffset
            Return If(row.GetValue (Of DateTimeOffset)(fieldName), Nothing)
        End Function

        Protected Function GetNullableDateTimeOffset(row As DataRow, fieldName As String) As DateTimeOffset?
            Return row.GetValue (Of DateTimeOffset)(fieldName)
        End Function

        Protected Function GetGuid(row As DataRow, fieldName As String) As Guid
            Return If(row.GetValue (Of Guid)(fieldName), Guid.Empty)
        End Function

        Protected Function GetNullableGuid(row As DataRow, fieldName As String) As Guid?
            Return row.GetValue (Of Guid)(fieldName)
        End Function

        Protected Function GetTimeSpan(row As DataRow, fieldName As String) As TimeSpan
            Return If(row.GetValue (Of TimeSpan)(fieldName), Nothing)
        End Function

        Protected Function GetNullableTimeSpan(row As DataRow, fieldName As String) As TimeSpan?
            Return row.GetValue (Of TimeSpan)(fieldName)
        End Function

        Protected Function GetXmlDocument(row As DataRow, fieldName As String) As XmlDocument
            Return New XmlDocument With {
                .InnerXml = If(row.Table.Columns.Contains(fieldName), row.GetText(fieldName), "")
                }
        End Function

        Protected Function GetString(row As DataRow, fieldName As String) As String
            Return If(row.Table.Columns.Contains(fieldName), row.GetText(fieldName), Nothing)
        End Function

        Private Function ToDataTable(cmd As SqlCommand) As DataTable
            Try
                Dim da = New SqlDataAdapter(cmd)
                Dim dt = New DataTable()
                da.Fill(dt)
                Return dt
            Catch ex As Exception
                Logger(ex)
                Return Nothing
            End Try
        End Function

        Protected Sub CreateStagingTable(tempTableName As String,
                                         Optional onlyPrimaryKeys As Boolean = False)
            Dim stagingSqlBuilder = New StringBuilder()
            stagingSqlBuilder.AppendLine("CREATE TABLE " & tempTableName & " (")

            For Each mergeColumn In _
                Columns.Where(Function(x) onlyPrimaryKeys AndAlso x.PrimaryKey OrElse Not onlyPrimaryKeys)
                stagingSqlBuilder.Append($"[{mergeColumn.ColumnName}] {mergeColumn.SqlDataTypeText} NULL")

                If Not mergeColumn.PrimaryKey Then
                    stagingSqlBuilder.AppendLine(",")
                    stagingSqlBuilder.Append($"[{mergeColumn.ColumnName}Changed] [BIT] NOT NULL")
                End If

                stagingSqlBuilder.AppendLine(If(mergeColumn IsNot Columns(Columns.Count - 1), ",", ")"))
            Next

            Dim stagingSql = stagingSqlBuilder.ToString()
            If HasInjection(stagingSql) Then Throw New Exception("Sql Injection attempted. Aborted")

            Using cn = New SqlConnection(ConnectionString)
                Dim cmd = New SqlCommand With {
                        .Connection = cn,
                        .CommandType = CommandType.Text,
                        .CommandText = stagingSql
                        }
                cn.Open()
                cmd.ExecuteNonQuery()
            End Using
        End Sub
    End Class

    Module Ext
        <Extension>
        Function GetValue (Of T As Structure)(row As DataRow, columnName As String) As T?
            If row.IsNull(columnName) OrElse Not row.Table.Columns.Contains(columnName) Then Return Nothing
            Return row(columnName)
        End Function

        <Extension>
        Function GetText(row As DataRow, columnName As String) As String
            If row.IsNull(columnName) OrElse Not row.Table.Columns.Contains(columnName) Then Return Nothing
            Return If(TryCast(row(columnName), String), Nothing)
        End Function
    End Module
End Namespace