Android Doc Util
================

A simple console app which can scrape Android Documentation from a given url, and transform the API Information into another useful format.


## Scraping

Scraping was built specifically with Google's Android Document format in mind and should work with Android Docs as well as Google Play Services docs.

**Usage:**

```
mono android-doc-util scrape --out ./docs --url https://developer.android.com/reference/ --package-filter "android.support"
```

**Options:**

 - `out` location to save the scraped documentation to.
 - `url` The url to scrape from, which should contain a `package-list` file in its root.
 - `package-filter` Only packages which start with one of the specified filters will be downloaded.  You can specify this option multiple times to add multiple filters to check for.
 - `threads` Experimental, by default only one thread is used.
 - `package-list-source` Can be used to specify an alternative file or url to discover package names from instead of using the `package-list` file in the root of the `url` specified.  This is useful as sometimes Google will not update their `package-list` with all the newest package names even though the actual documentation does exist for it.  You may want to augment their list locally and use it instead of the default for scraping.


## Transforming

**Usage:**

```
mono android-doc-util transform --out "./output.xml" --type Metadata  --dir ./docs --prefix "/reference/" --package-filter "android.support"
```

**Options:**

 - `dir` Input directory where the scraped Android Docs live to parse.
 - `out` Output file to save the transformation to.
 - `type` The type of transform to save.  Current options are `Metadata` for an Android binding project Metadata Transform compatible file, or `Xml` for a simple XML format which could be parsed by other utilities.
 - `prefix` In the HTML, URL's are used to determine package names.  Android's docs don't exist at the root of the url, but in a subfolder (`/reference/`) so we need to specify this prefix so it can be stripped out of the package names (otherwise we'll end up with something like `reference.android.support.v4.app`).
 - `package-filter` Only packages which start with one of the specified filters will be transformed.  You can specify this option multiple times to add multiple filters to check for.
 


