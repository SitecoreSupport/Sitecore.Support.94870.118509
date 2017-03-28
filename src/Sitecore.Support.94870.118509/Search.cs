using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Security;
using Sitecore.ItemWebApi.Facets;
using Sitecore.ItemWebApi.Pipelines.Search;
using System.Collections.Generic;
using System.Linq;
using Sitecore.Data.Items;
using Sitecore.ItemWebApi.Pipelines.Request;
using Sitecore.Support.ItemWebApi.Extensions;
using Sitecore.Web;

namespace Sitecore.Support.ItemWebApi.Pipelines.Request
{
    public class Search : RequestProcessor
    {
        private const string SearchParameterName = "search";

        private const string LanguageParameterName = "language";

        private const string ShowHiddenItemsParameterName = "showHiddenItems";

        public override void Process(RequestArgs args)
        {
            Diagnostics.Assert.ArgumentNotNull(args, "args");
            string text = WebUtil.GetQueryString("search", null);
            if (text == null)
            {
                if (!args.Context.HttpContext.Request.HasQueryStringFlag("search"))
                {
                    return;
                }
                text = string.Empty;
            }
            string text2 = WebUtil.GetQueryString("showHiddenItems", null);
            if (text2 == null)
            {
                text2 = string.Empty;
            }
            bool showHiddenItems = text2 == "true";
            string queryString = WebUtil.GetQueryString("language", string.Empty);
            if (!string.IsNullOrEmpty(queryString))
            {
                using (new Sitecore.Globalization.LanguageSwitcher(queryString))
                {
                    this.RunSearchPipeline(args, text, queryString, showHiddenItems);
                    return;
                }
            }
            this.RunSearchPipeline(args, text, queryString, showHiddenItems);
        }

        private void RunSearchPipeline(RequestArgs args, string searchText, string languageName, bool showHiddenItems)
        {
            Item searchDefinition = this.GetSearchDefinition(Context.Database);
            Item rootItem = this.GetRootItem(args, searchDefinition);
            string searchText2 = this.GetSearchText(searchDefinition, searchText);
            using (IProviderSearchContext providerSearchContext = ContentSearchManager.GetIndex(new SitecoreIndexableItem(rootItem)).CreateSearchContext(SearchSecurityOptions.Default))
            {
                Sitecore.ItemWebApi.Pipelines.Search.SearchArgs searchArgs = new Sitecore.ItemWebApi.Pipelines.Search.SearchArgs(providerSearchContext, searchDefinition, rootItem, searchText2, languageName, showHiddenItems);
                Sitecore.Pipelines.CorePipeline.Run("itemWebApiSearch", searchArgs);
                SearchResults<ConvertedSearchResultItem> results = searchArgs.Queryable.GetResults<ConvertedSearchResultItem>();
                args.Items = (from s in results.Hits
                              select s.Document.GetItem()).ToArray<Item>();
                args.CustomData["totalCount"] = results.TotalSearchResults;
                if (results.Facets != null)
                {
                    args.CustomData["facets"] = this.GetFacets(searchArgs.FacetProviders, results.Facets);
                }
            }
        }

        private IEnumerable<Facet> GetFacets(IList<IFacetProvider> facetProviders, FacetResults facets)
        {
            List<Facet> list = new List<Facet>();
            using (List<FacetCategory>.Enumerator enumerator = facets.Categories.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    FacetCategory facetCategory = enumerator.Current;
                    IFacetProvider facetProvider = facetProviders.FirstOrDefault((IFacetProvider provider) => provider.CanHandle(facetCategory.Name));
                    Facet facet;
                    if (facetProvider != null)
                    {
                        facet = facetProvider.GetFacet(facetCategory.Name);
                    }
                    else
                    {
                        facet = new Facet(facetCategory.Name);
                    }
                    foreach (ContentSearch.Linq.FacetValue current in (from v in facetCategory.Values
                                                                                orderby v.Name
                                                                                select v).ThenByDescending((Sitecore.ContentSearch.Linq.FacetValue value) => value.AggregateCount))
                    {
                        Sitecore.ItemWebApi.Facets.FacetValue value;
                        if (facetProvider != null)
                        {
                            string name = current.Name;
                            value = facetProvider.GetValue(facet, name);
                        }
                        else
                        {
                            value = new Sitecore.ItemWebApi.Facets.FacetValue(facet.Name + ":" + current.Name, current.Name, 0)
                            {
                                DisplayText = StringUtil.Capitalize(current.Name)
                            };
                        }
                        value.Count = current.AggregateCount;
                        if (facet.Values.Exists((Sitecore.ItemWebApi.Facets.FacetValue v) => v.Text == value.Text))
                        {
                            facet.Values.Find((Sitecore.ItemWebApi.Facets.FacetValue v) => v.Text == value.Text).Count += value.Count;
                        }
                        else
                        {
                            facet.Values.Add(value);
                        }
                    }
                    list.Add(facet);
                }
            }
            return list;
        }

        private Item GetRootItem(RequestArgs args, Item searchDescriptor)
        {
            string defaultValue = (searchDescriptor != null) ? searchDescriptor["root"] : WebUtil.GetQueryString("root", string.Empty);
            if (string.IsNullOrEmpty(defaultValue))
            {
                return Context.ContentDatabase.GetRootItem();
            }
            return Context.ContentDatabase.GetItem(defaultValue);
        }

        private Item GetSearchDefinition(Data.Database database)
        {
            string text = WebUtil.GetQueryString("searchConfig", null) ?? ParseSearchText.AllItemId.ToString();
            if (Data.ID.IsID(text))
            {
                Item item = database.GetItem(text);
                if (item != null)
                {
                    return item;
                }
            }
            Item item2 = database.GetItem(ParseSearchText.SearchId);
            if (item2 == null)
            {
                return null;
            }
            return item2.Children[text];
        }

        private string GetSearchText(Item searchDescriptor, string searchText)
        {
            if (searchDescriptor == null)
            {
                return searchText;
            }
            string text = string.Empty;
            if (!string.IsNullOrEmpty(searchDescriptor["Search"]))
            {
                text = searchDescriptor["Search"];
            }
            if (MainUtil.GetBool(searchDescriptor["AppendSearchText"], true) && !string.IsNullOrEmpty(searchText))
            {
                text += searchText;
            }
            return text;
        }
    }
}