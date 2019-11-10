### INFO
[AniDB MyList Spec](https://wiki.anidb.net/w/UDP_API_Definition#MyList_Commands)

This has all files related to handling the AniDB MyList. At the beginning, it will likely have references to it hardcoded throughout the project, but those should be abstracted out with scrobble events (which will go out to things like Trakt, AniList, etc).

### MyList Notes
#### The "official" way to handle it would follow as such:
##### Adding Files 
1. Add a hash with various known states and no `edit=1`
2. Parse the response
  - Already Exists: Store the MyList ID and update the state
  - Not Exists: Store the MyList ID, we are done for now

##### Updating Files
1. We *should* have a MyList ID, add the file with `lid={MyListID}&edit=1` and things to update
Done....

#### The problem:
Generic Files are the proper way to handle manual links and files that are added as `generic`. Generic Files are added via an anime ID and episode number.
Our current system makes this very difficult to do.
The AniDB MyList officially does not support using the ED2k to change MyList states. It does work, but....