// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using UnityEngine;
using Vertex.VSig;

namespace Vellocet.Sdk.VSig
{
    [Serializable]
    public sealed class VSigCompiledProgram
    {
        [SerializeField, HideInInspector, System.Runtime.Serialization.OptionalField] private byte[] vertexBytes;
        // Legacy Unity-serialized programs are Vertex compiled format 1 (line-only locations).
        [SerializeField] private int formatVersion = 1;
        [SerializeField] private string sceneName;
        [SerializeField] private bool hasRandomSeed;
        [SerializeField] private int randomSeed;
        [SerializeField] private VSigCompiledRef[] refs = Array.Empty<VSigCompiledRef>();
        [SerializeField] private VSigCompiledConstant[] constants = Array.Empty<VSigCompiledConstant>();
        [SerializeField] private VSigCompiledGroup[] groups = Array.Empty<VSigCompiledGroup>();
        [SerializeField] private VSigCompiledVariable[] variables = Array.Empty<VSigCompiledVariable>();
        [SerializeField] private VSigCompiledHandler[] handlers = Array.Empty<VSigCompiledHandler>();
        [SerializeField] private VSigCompiledProcedure[] procedures = Array.Empty<VSigCompiledProcedure>();
        [SerializeField] private VSigCompiledUse[] uses = Array.Empty<VSigCompiledUse>();
        [SerializeField] private VSigCompiledOutput[] outputs = Array.Empty<VSigCompiledOutput>();
        [SerializeField] private VSigCompiledStateExport[] stateExports = Array.Empty<VSigCompiledStateExport>();

        public bool HasContent =>
            RefCount > 0 || ConstantCount > 0 || GroupCount > 0 || VariableCount > 0 || HandlerCount > 0 ||
            ProcedureCount > 0 || UseCount > 0 || OutputCount > 0 || StateExportCount > 0;

        public int RefCount => refs?.Length ?? 0;
        public int ConstantCount => constants?.Length ?? 0;
        public int GroupCount => groups?.Length ?? 0;
        public int VariableCount => variables?.Length ?? 0;
        public int HandlerCount => handlers?.Length ?? 0;
        public int ProcedureCount => procedures?.Length ?? 0;
        public int UseCount => uses?.Length ?? 0;
        public int OutputCount => outputs?.Length ?? 0;
        public int StateExportCount => stateExports?.Length ?? 0;

        public static VSigCompiledProgram FromProgram(VSigProgram program) =>
            FromVertex(Vertex.VSig.VSigCompiledProgram.FromProgram(program ?? new VSigProgram()));

        public VSigProgram ToProgram() => ToVertex().ToProgram();

        public Vertex.VSig.VSigCompiledProgram ToVertex()
        {
            // Bound the recursive legacy object graph before a serializer can traverse it.
            var pending = new Stack<(VSigCompiledStatement Statement, int Depth)>();
            var total = 0;
            void Push(VSigCompiledStatement[] values, int depth)
            {
                if (values == null) return;
                if ((values.Length > 0 && depth > VSigSecurityLimits.MaximumStatementNestingDepth) ||
                    values.Length > VSigSecurityLimits.MaximumStatementsPerBlock ||
                    total > VSigSecurityLimits.MaximumStatementsPerProgram - values.Length)
                    throw new ArgumentException("Legacy VSig exceeds its statement or nesting limit.");
                total += values.Length;
                foreach (var value in values) if (value != null) pending.Push((value, depth));
            }
            if (HandlerCount > VSigSecurityLimits.MaximumHandlers || ProcedureCount > VSigSecurityLimits.MaximumProcedures)
                throw new ArgumentException("Legacy VSig exceeds its declaration limit.");
            foreach (var handler in handlers ?? Array.Empty<VSigCompiledHandler>()) Push(handler?.statements, 0);
            foreach (var procedure in procedures ?? Array.Empty<VSigCompiledProcedure>()) Push(procedure?.statements, 0);
            while (pending.Count > 0)
            {
                var (statement, depth) = pending.Pop();
                Push(statement.statements, depth + 1);
                Push(statement.elseStatements, depth + 1);
                if ((statement.cases?.Length ?? 0) > VSigSecurityLimits.MaximumStatementsPerBlock)
                    throw new ArgumentException("Legacy VSig exceeds its match-case limit.");
                foreach (var item in statement.cases ?? Array.Empty<VSigCompiledMatchCase>())
                    Push(item?.statements, depth + 1);
            }
            if (vertexBytes is { Length: > 0 }) return Vertex.VSig.VSigCompiledProgram.Deserialize(vertexBytes);
            // The fields have the same wire names as Vertex v1. DCS includes the private
            // [Serializable] fields, unlike JsonUtility's depth-limited recursive output.
            formatVersion = 1;
            using var stream = new MemoryStream();
            new DataContractJsonSerializer(typeof(VSigCompiledProgram),
                new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 1000000 }).WriteObject(stream, this);
            if (stream.Length > Vertex.VSig.VSigCompiledProgram.MaximumCompiledBytes)
                throw new ArgumentException("Legacy VSig exceeds its compiled byte limit.");
            return Vertex.VSig.VSigCompiledProgram.Deserialize(stream.ToArray());
        }

        public static VSigCompiledProgram FromVertex(Vertex.VSig.VSigCompiledProgram compiled)
        {
            if (compiled == null) return null;
            var bytes = compiled.Serialize();
            using var stream = new MemoryStream(bytes);
            var legacy = (VSigCompiledProgram)new DataContractJsonSerializer(typeof(VSigCompiledProgram),
                new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 1000000 }).ReadObject(stream);
            legacy.vertexBytes = bytes;
            return legacy;
        }

    }

    [Serializable]
    public sealed class VSigCompiledParameter
    {
        public int line;
        public string name;
        public string typeName;
        public string defaultExpression;
    }

    [Serializable]
    public sealed class VSigCompiledNamedArgument
    {
        public int line;
        public string name;
        public string expression;
    }

    [Serializable]
    public sealed class VSigCompiledUse
    {
        public int line;
        public string moduleName;
        public string alias;
        public string rootPath;
        public string rootAnchorMarkerGuid;
        public VSigCompiledNamedArgument[] arguments = Array.Empty<VSigCompiledNamedArgument>();
    }

    [Serializable]
    public sealed class VSigCompiledRef
    {
        public int line;
        public string alias;
        public string entityClassName;
        public string path;
        public string scopeAnchorMarkerGuid;
        public string scopeRootPath;
    }

    [Serializable]
    public sealed class VSigCompiledGroup
    {
        public int line;
        public string alias;
        public string[] members = Array.Empty<string>();
    }

    [Serializable]
    public sealed class VSigCompiledVariable
    {
        public int line;
        public string name;
        public string typeName;
        public string expression;
    }

    [Serializable]
    public sealed class VSigCompiledConstant
    {
        public int line;
        public string name;
        public string typeName;
        public string expression;
    }

    [Serializable]
    public sealed class VSigCompiledHandler
    {
        public int line;
        public bool once;
        public string authority;
        public string sourceAlias;
        public string outputName;
        public VSigCompiledParameter[] parameters = Array.Empty<VSigCompiledParameter>();
        public VSigCompiledStatement[] statements = Array.Empty<VSigCompiledStatement>();
    }

    [Serializable]
    public sealed class VSigCompiledProcedure
    {
        public int line;
        public string instanceAlias;
        public string name;
        public bool isInput;
        public VSigCompiledParameter[] parameters = Array.Empty<VSigCompiledParameter>();
        public VSigCompiledStatement[] statements = Array.Empty<VSigCompiledStatement>();
    }

    [Serializable]
    public sealed class VSigCompiledOutput
    {
        public int line;
        public string instanceAlias;
        public string name;
        public VSigCompiledParameter[] parameters = Array.Empty<VSigCompiledParameter>();
    }

    [Serializable]
    public sealed class VSigCompiledStateExport
    {
        public int line;
        public string instanceAlias;
        public string name;
        public string variableName;
        public string typeName;
    }

    [Serializable]
    public sealed class VSigCompiledMatchCase
    {
        public int line;
        public bool isDefault;
        public string expression;
        [SerializeReference] public VSigCompiledStatement[] statements = Array.Empty<VSigCompiledStatement>();
    }

    [Serializable]
    public sealed class VSigCompiledStatement
    {
        public VSigCompiledStatementKind kind;
        public int line;
        public string rawText;
        public string targetAlias;
        public VSigEntityTargetKind targetKind;
        public string memberName;
        public string taskName;
        public bool localToInstance;
        public bool repeats;
        public string[] arguments = Array.Empty<string>();
        public string expression;
        public string secondaryExpression;
        public string variableName;
        [SerializeReference] public VSigCompiledStatement[] statements = Array.Empty<VSigCompiledStatement>();
        [SerializeReference] public VSigCompiledStatement[] elseStatements = Array.Empty<VSigCompiledStatement>();
        [SerializeReference] public VSigCompiledMatchCase[] cases = Array.Empty<VSigCompiledMatchCase>();
    }

    public enum VSigCompiledStatementKind
    {
        Call,
        ProcedureCall,
        Wait,
        Assignment,
        If,
        Match,
        Emit,
        Schedule,
        Cancel,
        Unsupported,
        ServerCommand
    }
}
