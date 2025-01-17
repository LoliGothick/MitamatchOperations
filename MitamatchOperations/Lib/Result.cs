﻿using System;

namespace Mitama.Lib
{
    public record Result<T, E>;
    public record Ok<T, E>(T Value) : Result<T, E>;
    public record Err<T, E>(E Error) : Result<T, E>;

    public static class ResultExtensions
    {
        public static Result<T, E> Ok<T, E>(this T value) => new Ok<T, E>(value);
        public static Result<T, E> Err<T, E>(this E error) => new Err<T, E>(error);
        public static bool IsErr<T, E>(this Result<T, E> result) => result is Err<T, E>;
        public static bool IsOk<T, E>(this Result<T, E> result) => result is Ok<T, E>;
#pragma warning disable CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
        public static T Unwrap<T, E>(this Result<T, E> result) => result switch
        {
            Ok<T, E> ok => ok.Value,
            Err<T, E> err => throw new InvalidOperationException(err.Error.ToString())
        };
        public static T UnwrapOr<T, E>(this Result<T, E> result, T defaultValue) => result switch
        {
            Ok<T, E> ok => ok.Value,
            Err<T, E> err => defaultValue
        };
        public static T UnwrapOrElse<T, E>(this Result<T, E> result, Func<E, T> f) => result switch
        {
            Ok<T, E> ok => ok.Value,
            Err<T, E> err => f(err.Error)
        };
        public static E UnwrapErr<T, E>(this Result<T, E> result) => result switch
        {
            Ok<T, E> ok => throw new InvalidOperationException(ok.Value.ToString()),
            Err<T, E> err => err.Error
        };
        public static Result<U, E> Map<T, E, U>(this Result<T, E> result, Func<T, U> f) => result switch
        {
            Ok<T, E> ok => f(ok.Value).Ok<U, E>(),
            Err<T, E> err => err.Error.Err<U, E>()
        };
        public static Result<T, F> MapErr<T, E, F>(this Result<T, E> result, Func<E, F> f) => result switch
        {
            Ok<T, E> ok => ok.Value.Ok<T, F>(),
            Err<T, E> err => f(err.Error).Err<T, F>()
        };
        public static Result<U, E> AndThen<T, E, U>(this Result<T, E> result, Func<T, Result<U, E>> f) => result switch
        {
            Ok<T, E> ok => f(ok.Value),
            Err<T, E> err => err.Error.Err<U, E>()
        };
        public static Result<T, F> OrElse<T, E, F>(this Result<T, E> result, Func<E, Result<T, F>> f) => result switch
        {
            Ok<T, E> ok => ok.Value.Ok<T, F>(),
            Err<T, E> err => f(err.Error)
        };
        public static Result<U, E> MapOr<T, E, U>(this Result<T, E> result, Func<E, U> f, Func<T, U> g) => result switch
        {
            Ok<T, E> ok => g(ok.Value).Ok<U, E>(),
            Err<T, E> err => f(err.Error).Ok<U, E>()
        };
        public static Result<U, F> MapOrElse<T, E, U, F>(this Result<T, E> result, Func<E, U> f, Func<T, U> g) => result switch
        {
            Ok<T, E> ok => g(ok.Value).Ok<U, F>(),
            Err<T, E> err => f(err.Error).Ok<U, F>()
        };
#pragma warning restore CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
    }
}
