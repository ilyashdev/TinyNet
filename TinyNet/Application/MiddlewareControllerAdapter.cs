﻿using TinyNet.Controllers;
using TinyNet.DI;
using TinyNet.Http;

namespace TinyNet.Application;

public class MiddlewareControllerAdapter
{
    private readonly ControllerHandler _controllerHandler;
    private readonly DIScope _scope;

    public MiddlewareControllerAdapter(ControllerHandler controllerHandler, DIScope scope)
    {
        _controllerHandler = controllerHandler;
        _scope = scope;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        await _controllerHandler.Handle(httpContext, _scope);
    }
}