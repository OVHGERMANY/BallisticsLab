using System;
using System.Reflection;
using EFT;
using EFT.Ballistics;
using EFT.InputSystem;
using UnityEngine;

namespace BallisticsLab.Runtime
{
    internal static class TargetMethodResolver
    {
        private static readonly BindingFlags ExactInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        internal static MethodInfo ResolveHandleCollision()
        {
            return Require(
                typeof(Shot).GetMethod(
                    "HandleCollision",
                    ExactInstance,
                    null,
                    new[] { typeof(float), typeof(Vector3), typeof(Vector3) },
                    null),
                typeof(Shot),
                typeof(void),
                new[] { typeof(float), typeof(Vector3), typeof(Vector3) },
                "Shot.HandleCollision(float, Vector3, Vector3)");
        }

        internal static MethodInfo ResolveCreateFragments()
        {
            return Require(
                typeof(Shot).GetMethod("CreateFragments", ExactInstance, null, Type.EmptyTypes, null),
                typeof(Shot),
                typeof(void),
                Type.EmptyTypes,
                "Shot.CreateFragments()");
        }

        internal static MethodInfo ResolveShotDelegate()
        {
            return Require(
                typeof(ClientGameWorld).GetMethod(
                    "ShotDelegate",
                    ExactInstance,
                    null,
                    new[] { typeof(Shot) },
                    null),
                typeof(ClientGameWorld),
                typeof(void),
                new[] { typeof(Shot) },
                "ClientGameWorld.ShotDelegate(Shot)");
        }

        internal static MethodInfo ResolvePlayerCommand()
        {
            return Require(
                typeof(PlayerOwner).GetMethod(
                    "TranslateCommand",
                    ExactInstance,
                    null,
                    new[] { typeof(ECommand) },
                    null),
                typeof(PlayerOwner),
                typeof(InputNode.ETranslateResult),
                new[] { typeof(ECommand) },
                "PlayerOwner.TranslateCommand(ECommand)");
        }

        private static MethodInfo Require(
            MethodInfo method,
            Type declaringType,
            Type returnType,
            Type[] parameters,
            string name)
        {
            if (method == null || method.DeclaringType != declaringType || method.IsStatic || method.ReturnType != returnType)
            {
                throw new MissingMethodException("Required SPT 4.1.2 method was not found: " + name);
            }

            ParameterInfo[] actual = method.GetParameters();
            if (actual.Length != parameters.Length)
            {
                throw new MissingMethodException("Required SPT 4.1.2 signature changed: " + name);
            }

            for (int index = 0; index < actual.Length; index++)
            {
                if (actual[index].ParameterType != parameters[index])
                {
                    throw new MissingMethodException("Required SPT 4.1.2 signature changed: " + name);
                }
            }

            return method;
        }
    }
}
