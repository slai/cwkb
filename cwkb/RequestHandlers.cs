using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NDjango;
using Nancy;

namespace cwkb
{
    public class RequestHandlers : NancyModule
    {
        public RequestHandlers()
        {
            Get["/"] = HandleErrors(x =>
            {
                var context = new Dictionary<string, object>();
                context["Title"] = "hello world!";

                return RenderTemplate("templates/index.html", context);
            });

            Get["/search"] = HandleErrors(x =>
            {
                var queryParts = HttpContext.Current.Request.QueryString;
                var context = new Dictionary<string, object>();
                string templateName = "templates/search.partial.html";
                var maxResults = int.Parse(System.Web.Configuration.WebConfigurationManager.AppSettings["maxResults"]);

                using (var db = new DataAccess())
                {
                    context["KBLastUpdateDate"] = db.GetKBLastUpdateDate();

                    if (queryParts.AllKeys.Contains("q"))
                    {
                        // do search.
                        var query = SearchUtil.MassageQuery(queryParts["q"]);
                        context["Query"] = query;
                        try
                        {
                            var results = db.PerformSearch(query, maxResults + 1);
                            var queryWords = SearchUtil.GetQueryWords(query);
                            // set excerpts
                            foreach(var r in results)
                                r.Item.SetExcerpt(queryWords);

                            context["Results"] = results;
                            context["MoreThanMax"] = results.Count > maxResults;
                            context["QueryWords"] = queryWords;
                        }
                        catch (Exception ex)
                        {
                            // TODO: log
                            context["QueryError"] = true;
                            // don't allow query to be rendered to avoid injection
                            context["Query"] = "";
                        }
                        context["MaxResults"] = maxResults;
                        
                        templateName = "templates/results.partial.html";
                    }
                }

                return RenderTemplate(templateName, context);
            });

            Get["/timeentry/{id}"] = HandleErrors(x =>
            {
                var context = new Dictionary<string, object>();

                using (var db = new DataAccess())
                {
                    int id;
                    if (!int.TryParse(x.id, out id))
                        throw new HttpException(404, "A time entry with an id of " + x.id + " could not be found.");

                    TimeEntry e = db.GetTimeEntry(id);
                    if (e == null)
                        throw new HttpException(404, "A time entry with an id of " + x.id + " could not be found.");

                    context["TimeEntry"] = e;
                }

                return RenderTemplate("templates/timeentry.partial.html", context);
            });

            Get["/kbarticle/{id}"] = HandleErrors(x =>
            {
                var context = new Dictionary<string, object>();

                using (var db = new DataAccess())
                {
                    int id;
                    if (!int.TryParse(x.id, out id))
                        throw new HttpException(404, "An KB article with an id of " + x.id + " could not be found.");

                    KBArticle a = db.GetKBArticle(id);
                    if (a == null)
                        throw new HttpException(404, "An KB article with an id of " + x.id + " could not be found.");

                    context["KBArticle"] = a;
                }

                return RenderTemplate("templates/kbarticle.partial.html", context);
            });

            Get["/ticket/{id}"] = HandleErrors(x =>
            {
                var context = new Dictionary<string, object>();

                using (var db = new DataAccess())
                {
                    int id;
                    if (!int.TryParse(x.id, out id))
                        throw new HttpException(404, "A ticket with an id of " + x.id + " could not be found.");

                    Ticket t = db.GetTicket(id, loadNotes : true, loadTimeEntries : true);
                    if (t == null)
                        throw new HttpException(404, "A ticket with an id of " + x.id + " could not be found.");

                    context["Ticket"] = t;
                }

                var query = HttpContext.Current.Request.QueryString;
                if (query.AllKeys.Contains("te"))
                {
                    int teId;
                    if (int.TryParse(query["te"], out teId))
                        context["JumpToTimeEntryId"] = teId;
                }

                return RenderTemplate("templates/ticket.partial.html", context);
            });

            // don't handle errors for this; let it propagate to the browser
            Get["/debug"] = x =>
            {
                var context = new Dictionary<string, object>();

                using (var db = new DataAccess())
                {
                    context["CatalogStatus"] = db.GetFTSCatalogStatus();
                    context["KB_Resolution"] = db.GetFTSTableStatus("KB_Resolution");
                    context["SR_Detail"] = db.GetFTSTableStatus("SR_Detail");
                    context["SR_Service"] = db.GetFTSTableStatus("SR_Service");
                    context["Time_Entry"] = db.GetFTSTableStatus("Time_Entry");
                }

                return RenderTemplate("templates/debug.html", context);
            };
        }

        private string RenderTemplate(string templateName, Dictionary<string, object> context)
        {
            var provider = new TemplateManagerProvider().WithLoader(new FileSystemTemplateLoader());
            provider = provider.WithFilters(NDjango.FiltersCS.FilterManager.GetFilters()).WithFilter("highlight", new HighlightFilter());
            var manager = provider.GetNewManager();

            System.IO.TextReader reader = manager.RenderTemplate(templateName, context);
            return reader.ReadToEnd();
        }

        private Response Response404(string message)
        {
            var context = new Dictionary<string, object>();
            context["Message"] = message;

            Response r = (Response)RenderTemplate("templates/404.partial.html", context);
            r.StatusCode = Nancy.HttpStatusCode.NotFound;

            return r;
        }

        private Response Response404()
        {
            return Response404("Object not found.");
        }

        private Response Response500(string message)
        {
            var context = new Dictionary<string, object>();
            context["Message"] = message;

            Response r = (Response)RenderTemplate("templates/500.partial.html", context);
            r.StatusCode = Nancy.HttpStatusCode.InternalServerError;

            return r;
        }

        private Response Response500()
        {
            return Response500("Internal server error.");
        }

        private Func<dynamic, Response> HandleErrors(Func<dynamic, Response> f)
        {
            return x =>
            {
                try
                {
                    return f(x);
                }
                catch (HttpException e)
                {
                    if (e.GetHttpCode() == 404)
                        return Response404(e.GetHtmlErrorMessage());
                    else if (e.GetHttpCode() == 500)
                        return Response500(e.GetHtmlErrorMessage());
                    else
                        // TODO: cannot handle other status codes
                        return Response500(e.GetHttpCode() + " - " + e.GetHtmlErrorMessage());

                }
                catch (Exception e)
                {
                    return Response500(e.Message);
                }
            };
        }
    }
}