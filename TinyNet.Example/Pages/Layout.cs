using System.Net;

namespace TinyNet.Example.Pages;

public static class Layout
{
    public static string Page(string title, string requestId, string body) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{Encode(title)}}</title>
          <style>
            body { font-family: system-ui, sans-serif; max-width: 40rem; margin: 2rem auto; padding: 0 1rem; color: #222; }
            form.add { display: grid; gap: .5rem; margin-bottom: 2rem; }
            input, textarea, button { font: inherit; padding: .4rem .6rem; }
            .note { display: flex; justify-content: space-between; gap: 1rem; border-top: 1px solid #ddd; padding: .75rem 0; }
            .note p { margin: .25rem 0 0; color: #555; white-space: pre-wrap; }
            .error { color: #b00020; }
            footer { margin-top: 2rem; color: #888; font-size: .85rem; }
          </style>
        </head>
        <body>
          <h1>{{Encode(title)}}</h1>
          {{body}}
          <footer>request {{requestId}}</footer>
        </body>
        </html>
        """;

    public static string Encode(string text) => WebUtility.HtmlEncode(text);
}