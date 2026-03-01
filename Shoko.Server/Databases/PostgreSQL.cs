using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using FluentNHibernate.Cfg;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Driver;
using NHibernate.SqlCommand;
using NHibernate.SqlTypes;
using Npgsql;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

// ReSharper disable InconsistentNaming


namespace Shoko.Server.Databases;

public class PostgreSQL : BaseDatabase<NpgsqlConnection>
{
    public override string Name { get; } = "PostgreSQL";
    public override int RequiredVersion { get; } = 1;

    private List<DatabaseCommand> createDatabase = new()
    {
        new DatabaseCommand(0, 0, "CREATE DATABASE {schema} WITH OWNER = {owner} ENCODING = 'UTF8' LOCALE_PROVIDER = 'libc' CONNECTION LIMIT = -1 IS_TEMPLATE = False;"),

    };
    private List<DatabaseCommand> createSchema = new()
    {
        new DatabaseCommand(0, 1,"CREATE SCHEMA {schema};"),
        new DatabaseCommand(0, 2,"ALTER SCHEMA {schema} OWNER TO {owner};")
    };

    private string setup =
        "SET statement_timeout = 0; SET lock_timeout = 0; SET idle_in_transaction_session_timeout = 0; SET client_encoding = 'UTF8'; SET standard_conforming_strings = on; SELECT pg_catalog.set_config('search_path', '', false); SET check_function_bodies = false; SET xmloption = content; SET client_min_messages = warning; SET row_security = off; SET default_tablespace = ''; SET default_table_access_method = heap; ";


    private List<DatabaseCommand> createVersionTable = new()
    {
        new DatabaseCommand(0, 3,"CREATE TABLE {schema}.versions ( versionsid integer NOT NULL, versiontype character varying(100) NOT NULL, versionvalue character varying(100) NOT NULL, versionrevision character varying(100) DEFAULT NULL::character varying, versioncommand text, versionprogram character varying(100) DEFAULT NULL::character varying);"),
        new DatabaseCommand(0, 4,"ALTER TABLE {schema}.versions OWNER TO {owner};"),
        new DatabaseCommand(0, 5,"CREATE SEQUENCE {schema}.versions_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(0, 6,"ALTER SEQUENCE {schema}.versions_seq OWNER TO {owner};"),
        new DatabaseCommand(0, 7,"ALTER SEQUENCE {schema}.versions_seq OWNED BY {schema}.versions.versionsid;"),
        new DatabaseCommand(0, 8,"ALTER TABLE ONLY {schema}.versions ALTER COLUMN versionsid SET DEFAULT nextval('{schema}.versions_seq'::regclass);"),
        new DatabaseCommand(0, 9,"ALTER TABLE ONLY {schema}.versions ADD CONSTRAINT pk_versions PRIMARY KEY (versionsid);"),
        new DatabaseCommand(0, 10,"CREATE INDEX ix_versions_versiontype ON {schema}.versions USING btree (versiontype, versionvalue, versionrevision);"),
    };

    private List<DatabaseCommand> createTables = new()
    {
        new DatabaseCommand(1, -1,"CREATE FUNCTION {schema}.on_update_current_timestamp_videolocal_user() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN NEW.lastupdated = now(); RETURN NEW; END; $$;"),
        new DatabaseCommand(1, -1,"ALTER FUNCTION {schema}.on_update_current_timestamp_videolocal_user() OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_anime ( anidb_animeid integer NOT NULL, animeid integer NOT NULL, episodecount integer NOT NULL, airdate timestamp with time zone, enddate timestamp with time zone, url text, picname text, beginyear integer NOT NULL, endyear integer NOT NULL, animetype integer NOT NULL, maintitle character varying(500) DEFAULT NULL::character varying, alltitles character varying(1500) DEFAULT NULL::character varying, alltags text, description text, episodecountnormal integer NOT NULL, episodecountspecial integer NOT NULL, rating integer NOT NULL, votecount integer NOT NULL, temprating integer NOT NULL, tempvotecount integer NOT NULL, avgreviewrating integer NOT NULL, reviewcount integer NOT NULL, datetimeupdated timestamp with time zone NOT NULL, datetimedescupdated timestamp with time zone NOT NULL, imageenabled integer NOT NULL, restricted integer NOT NULL, annid integer, allcinemaid integer, latestepisodenumber integer, site_jp text, site_en text, wikipedia_id text, wikipediajp_id text, syoboiid integer, anisonid integer, crunchyrollid text, vndbid integer, bangumiid integer, funimationid text, hidiveid text, lainid integer);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_anime OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_anime_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_seq OWNED BY {schema}.anidb_anime.anidb_animeid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_anime_character ( anidb_anime_characterid integer NOT NULL, animeid integer NOT NULL, charid integer NOT NULL, chartype character varying(100) NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_anime_character OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_anime_character_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_character_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_character_seq OWNED BY {schema}.anidb_anime_character.anidb_anime_characterid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_anime_defaultimage ( anidb_anime_defaultimageid integer NOT NULL, animeid integer NOT NULL, imageparentid integer NOT NULL, imageparenttype integer NOT NULL, imagetype integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_anime_defaultimage OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_anime_defaultimage_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_defaultimage_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_defaultimage_seq OWNED BY {schema}.anidb_anime_defaultimage.anidb_anime_defaultimageid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_anime_relation ( anidb_anime_relationid integer NOT NULL, animeid integer NOT NULL, relatedanimeid integer NOT NULL, relationtype text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_anime_relation OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_anime_relation_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_relation_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_relation_seq OWNED BY {schema}.anidb_anime_relation.anidb_anime_relationid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_anime_similar ( anidb_anime_similarid integer NOT NULL, animeid integer NOT NULL, similaranimeid integer NOT NULL, approval integer NOT NULL, total integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_anime_similar OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_anime_similar_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_similar_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_similar_seq OWNED BY {schema}.anidb_anime_similar.anidb_anime_similarid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_anime_staff ( anidb_anime_staffid integer NOT NULL, animeid integer NOT NULL, creatorid integer NOT NULL, creatortype character varying(50) NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_anime_staff OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_anime_staff_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_staff_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_staff_seq OWNED BY {schema}.anidb_anime_staff.anidb_anime_staffid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_anime_tag ( anidb_anime_tagid integer NOT NULL, animeid integer NOT NULL, tagid integer NOT NULL, weight integer, localspoiler integer DEFAULT 0 NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_anime_tag OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_anime_tag_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_tag_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_tag_seq OWNED BY {schema}.anidb_anime_tag.anidb_anime_tagid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_anime_title ( anidb_anime_titleid integer NOT NULL, animeid integer NOT NULL, titletype character varying(50) DEFAULT NULL::character varying, language character varying(50) DEFAULT NULL::character varying, title character varying(500) DEFAULT NULL::character varying);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_anime_title OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_anime_title_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_title_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_anime_title_seq OWNED BY {schema}.anidb_anime_title.anidb_anime_titleid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_animeupdate ( anidb_animeupdateid integer NOT NULL, animeid integer NOT NULL, updatedat timestamp with time zone NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_animeupdate OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_animeupdate_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_animeupdate_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_animeupdate_seq OWNED BY {schema}.anidb_animeupdate.anidb_animeupdateid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_character ( anidb_characterid integer NOT NULL, charid integer NOT NULL, charname text, picname character varying(100) NOT NULL, charkanjiname text, chardescription text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_character OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_character_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_character_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_character_seq OWNED BY {schema}.anidb_character.anidb_characterid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_character_seiyuu ( anidb_character_seiyuuid integer NOT NULL, charid integer NOT NULL, seiyuuid integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_character_seiyuu OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_character_seiyuu_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_character_seiyuu_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_character_seiyuu_seq OWNED BY {schema}.anidb_character_seiyuu.anidb_character_seiyuuid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_episode ( anidb_episodeid integer NOT NULL, episodeid integer NOT NULL, animeid integer NOT NULL, lengthseconds integer NOT NULL, rating character varying(200) NOT NULL, votes character varying(200) NOT NULL, episodenumber integer NOT NULL, episodetype integer NOT NULL, airdate integer NOT NULL, datetimeupdated timestamp with time zone NOT NULL, description text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_episode OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_episode_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_episode_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_episode_seq OWNED BY {schema}.anidb_episode.anidb_episodeid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_episode_title ( anidb_episode_titleid integer NOT NULL, anidb_episodeid integer NOT NULL, language character varying(50) DEFAULT NULL::character varying, title text NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_episode_title OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_episode_title_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_episode_title_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_episode_title_seq OWNED BY {schema}.anidb_episode_title.anidb_episode_titleid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_file ( anidb_fileid integer NOT NULL, fileid integer NOT NULL, hash character varying(50) NOT NULL, groupid integer NOT NULL, file_source character varying(200) NOT NULL, file_description text, file_releasedate integer NOT NULL, datetimeupdated timestamp with time zone NOT NULL, filename text, filesize bigint NOT NULL, fileversion integer NOT NULL, iscensored boolean, isdeprecated boolean NOT NULL, internalversion integer NOT NULL, ischaptered boolean NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_file OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_file_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_file_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_file_seq OWNED BY {schema}.anidb_file.anidb_fileid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_fileupdate ( anidb_fileupdateid integer NOT NULL, filesize bigint NOT NULL, hash character varying(50) NOT NULL, hasresponse boolean NOT NULL, updatedat timestamp with time zone NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_fileupdate OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_fileupdate_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_fileupdate_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_fileupdate_seq OWNED BY {schema}.anidb_fileupdate.anidb_fileupdateid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_groupstatus ( anidb_groupstatusid integer NOT NULL, animeid integer NOT NULL, groupid integer NOT NULL, groupname text, completionstate integer NOT NULL, lastepisodenumber integer NOT NULL, rating numeric(6,2) DEFAULT NULL::numeric, votes integer NOT NULL, episoderange text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_groupstatus OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_groupstatus_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_groupstatus_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_groupstatus_seq OWNED BY {schema}.anidb_groupstatus.anidb_groupstatusid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_recommendation ( anidb_recommendationid integer NOT NULL, animeid integer NOT NULL, userid integer NOT NULL, recommendationtype integer NOT NULL, recommendationtext text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_recommendation OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_recommendation_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_recommendation_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_recommendation_seq OWNED BY {schema}.anidb_recommendation.anidb_recommendationid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_releasegroup ( anidb_releasegroupid integer NOT NULL, groupid integer NOT NULL, rating integer NOT NULL, votes integer NOT NULL, animecount integer NOT NULL, filecount integer NOT NULL, groupname text, groupnameshort text, ircchannel character varying(200) DEFAULT NULL::character varying, ircserver character varying(200) DEFAULT NULL::character varying, url text, picname character varying(50) NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_releasegroup OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_releasegroup_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_releasegroup_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_releasegroup_seq OWNED BY {schema}.anidb_releasegroup.anidb_releasegroupid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_seiyuu ( anidb_seiyuuid integer NOT NULL, seiyuuid integer NOT NULL, seiyuuname text, picname character varying(100) NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_seiyuu OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_seiyuu_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_seiyuu_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_seiyuu_seq OWNED BY {schema}.anidb_seiyuu.anidb_seiyuuid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_tag ( anidb_tagid integer NOT NULL, tagid integer NOT NULL, globalspoiler integer NOT NULL, tagname character varying(150) DEFAULT NULL::character varying, tagdescription text, verified integer DEFAULT 0 NOT NULL, parenttagid integer, tagnameoverride character varying(150) DEFAULT NULL::character varying, lastupdated timestamp with time zone DEFAULT '1970-01-01 00:00:00-03'::timestamp with time zone NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_tag OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_tag_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_tag_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_tag_seq OWNED BY {schema}.anidb_tag.anidb_tagid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.anidb_vote ( anidb_voteid integer NOT NULL, entityid integer NOT NULL, votevalue integer NOT NULL, votetype integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.anidb_vote OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.anidb_vote_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_vote_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.anidb_vote_seq OWNED BY {schema}.anidb_vote.anidb_voteid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.animecharacter ( characterid integer NOT NULL, anidbid integer NOT NULL, name text, alternatename text, description text, imagepath text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.animecharacter OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.animecharacter_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animecharacter_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animecharacter_seq OWNED BY {schema}.animecharacter.characterid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.animeepisode ( animeepisodeid integer NOT NULL, animeseriesid integer NOT NULL, anidb_episodeid integer NOT NULL, datetimeupdated timestamp with time zone NOT NULL, datetimecreated timestamp with time zone NOT NULL, ishidden integer DEFAULT 0 NOT NULL, episodenameoverride text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.animeepisode OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.animeepisode_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animeepisode_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animeepisode_seq OWNED BY {schema}.animeepisode.animeepisodeid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.animeepisode_user ( animeepisode_userid integer NOT NULL, jmmuserid integer NOT NULL, animeepisodeid integer NOT NULL, animeseriesid integer NOT NULL, watcheddate timestamp with time zone, playedcount integer NOT NULL, watchedcount integer NOT NULL, stoppedcount integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.animeepisode_user OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.animeepisode_user_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animeepisode_user_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animeepisode_user_seq OWNED BY {schema}.animeepisode_user.animeepisode_userid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.animegroup ( animegroupid integer NOT NULL, animegroupparentid integer, groupname text, description text, ismanuallynamed integer NOT NULL, datetimeupdated timestamp with time zone NOT NULL, datetimecreated timestamp with time zone NOT NULL, missingepisodecount integer NOT NULL, missingepisodecountgroups integer NOT NULL, overridedescription integer NOT NULL, episodeaddeddate timestamp with time zone, defaultanimeseriesid integer, latestepisodeairdate timestamp with time zone, mainanidbanimeid integer);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.animegroup OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.animegroup_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animegroup_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animegroup_seq OWNED BY {schema}.animegroup.animegroupid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.animegroup_user ( animegroup_userid integer NOT NULL, jmmuserid integer NOT NULL, animegroupid integer NOT NULL, isfave integer NOT NULL, unwatchedepisodecount integer NOT NULL, watchedepisodecount integer NOT NULL, watcheddate timestamp with time zone, playedcount integer NOT NULL, watchedcount integer NOT NULL, stoppedcount integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.animegroup_user OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.animegroup_user_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animegroup_user_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animegroup_user_seq OWNED BY {schema}.animegroup_user.animegroup_userid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.animeseries ( animeseriesid integer NOT NULL, animegroupid integer NOT NULL, anidb_id integer NOT NULL, datetimeupdated timestamp with time zone NOT NULL, datetimecreated timestamp with time zone NOT NULL, defaultaudiolanguage character varying(50) DEFAULT NULL::character varying, defaultsubtitlelanguage character varying(50) DEFAULT NULL::character varying, missingepisodecount integer NOT NULL, missingepisodecountgroups integer NOT NULL, latestlocalepisodenumber integer NOT NULL, episodeaddeddate timestamp with time zone, seriesnameoverride text, defaultfolder text, latestepisodeairdate timestamp with time zone, airson text, updatedat timestamp with time zone DEFAULT '2000-01-01 00:00:00-03'::timestamp with time zone NOT NULL, disableautomatchflags integer DEFAULT 0 NOT NULL, hiddenmissingepisodecount integer DEFAULT 0 NOT NULL, hiddenmissingepisodecountgroups integer DEFAULT 0 NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.animeseries OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.animeseries_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animeseries_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animeseries_seq OWNED BY {schema}.animeseries.animeseriesid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.animeseries_user ( animeseries_userid integer NOT NULL, jmmuserid integer NOT NULL, animeseriesid integer NOT NULL, unwatchedepisodecount integer NOT NULL, watchedepisodecount integer NOT NULL, watcheddate timestamp with time zone, playedcount integer NOT NULL, watchedcount integer NOT NULL, stoppedcount integer NOT NULL, lastepisodeupdate timestamp with time zone, hiddenunwatchedepisodecount integer DEFAULT 0 NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.animeseries_user OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.animeseries_user_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animeseries_user_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animeseries_user_seq OWNED BY {schema}.animeseries_user.animeseries_userid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.animestaff ( staffid integer NOT NULL, anidbid integer NOT NULL, name text, alternatename text, description text, imagepath text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.animestaff OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.animestaff_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animestaff_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.animestaff_seq OWNED BY {schema}.animestaff.staffid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.authtokens ( authid integer NOT NULL, userid integer NOT NULL, devicename text, token text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.authtokens OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.authtokens_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.authtokens_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.authtokens_seq OWNED BY {schema}.authtokens.authid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.bookmarkedanime ( bookmarkedanimeid integer NOT NULL, animeid integer NOT NULL, priority integer NOT NULL, notes text, downloading integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.bookmarkedanime OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.bookmarkedanime_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.bookmarkedanime_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.bookmarkedanime_seq OWNED BY {schema}.bookmarkedanime.bookmarkedanimeid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_anidb_mal ( crossref_anidb_malid integer NOT NULL, animeid integer NOT NULL, malid integer NOT NULL, maltitle text, startepisodetype integer NOT NULL, startepisodenumber integer NOT NULL, crossrefsource integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_anidb_mal OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_anidb_mal_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_mal_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_mal_seq OWNED BY {schema}.crossref_anidb_mal.crossref_anidb_malid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_anidb_other ( crossref_anidb_otherid integer NOT NULL, animeid integer NOT NULL, crossrefid character varying(100) DEFAULT NULL::character varying, crossrefsource integer NOT NULL, crossreftype integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_anidb_other OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_anidb_other_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_other_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_other_seq OWNED BY {schema}.crossref_anidb_other.crossref_anidb_otherid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_anidb_trakt_episode ( crossref_anidb_trakt_episodeid integer NOT NULL, animeid integer NOT NULL, anidbepisodeid integer NOT NULL, traktid character varying(100) DEFAULT NULL::character varying, season integer NOT NULL, episodenumber integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_anidb_trakt_episode OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_anidb_trakt_episode_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_trakt_episode_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_trakt_episode_seq OWNED BY {schema}.crossref_anidb_trakt_episode.crossref_anidb_trakt_episodeid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_anidb_traktv2 ( crossref_anidb_traktv2id integer NOT NULL, animeid integer NOT NULL, anidbstartepisodetype integer NOT NULL, anidbstartepisodenumber integer NOT NULL, traktid character varying(100) DEFAULT NULL::character varying, traktseasonnumber integer NOT NULL, traktstartepisodenumber integer NOT NULL, trakttitle text, crossrefsource integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_anidb_traktv2 OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_anidb_traktv2_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_traktv2_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_traktv2_seq OWNED BY {schema}.crossref_anidb_traktv2.crossref_anidb_traktv2id;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_anidb_tvdb ( crossref_anidb_tvdbid integer NOT NULL, anidbid integer NOT NULL, tvdbid integer NOT NULL, crossrefsource integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_anidb_tvdb OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_anidb_tvdb_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_tvdb_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_tvdb_seq OWNED BY {schema}.crossref_anidb_tvdb.crossref_anidb_tvdbid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_anidb_tvdb_episode ( crossref_anidb_tvdb_episodeid integer NOT NULL, anidbepisodeid integer NOT NULL, tvdbepisodeid integer NOT NULL, matchrating integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_anidb_tvdb_episode OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_anidb_tvdb_episode_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_tvdb_episode_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_tvdb_episode_seq OWNED BY {schema}.crossref_anidb_tvdb_episode.crossref_anidb_tvdb_episodeid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_anidb_tvdb_episode_override ( crossref_anidb_tvdb_episode_overrideid integer NOT NULL, anidbepisodeid integer NOT NULL, tvdbepisodeid integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_anidb_tvdb_episode_override OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_anidb_tvdb_episode_override_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_tvdb_episode_override_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anidb_tvdb_episode_override_seq OWNED BY {schema}.crossref_anidb_tvdb_episode_override.crossref_anidb_tvdb_episode_overrideid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_anime_staff ( crossref_anime_staffid integer NOT NULL, anidb_animeid integer NOT NULL, staffid integer NOT NULL, role text, roleid integer, roletype integer NOT NULL, language text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_anime_staff OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_anime_staff_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anime_staff_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_anime_staff_seq OWNED BY {schema}.crossref_anime_staff.crossref_anime_staffid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_customtag ( crossref_customtagid integer NOT NULL, customtagid integer NOT NULL, crossrefid integer NOT NULL, crossreftype integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_customtag OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_customtag_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_customtag_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_customtag_seq OWNED BY {schema}.crossref_customtag.crossref_customtagid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_file_episode ( crossref_file_episodeid integer NOT NULL, hash character varying(50) DEFAULT NULL::character varying, filename text, filesize bigint NOT NULL, crossrefsource integer NOT NULL, animeid integer NOT NULL, episodeid integer NOT NULL, percentage integer NOT NULL, episodeorder integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_file_episode OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_file_episode_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_file_episode_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_file_episode_seq OWNED BY {schema}.crossref_file_episode.crossref_file_episodeid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_languages_anidb_file ( crossref_languages_anidb_fileid integer NOT NULL, fileid integer NOT NULL, languagename character varying(100) DEFAULT ''::character varying NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_languages_anidb_file OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_languages_anidb_file_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_languages_anidb_file_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_languages_anidb_file_seq OWNED BY {schema}.crossref_languages_anidb_file.crossref_languages_anidb_fileid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.crossref_subtitles_anidb_file ( crossref_subtitles_anidb_fileid integer NOT NULL, fileid integer NOT NULL, languagename character varying(100) DEFAULT ''::character varying NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.crossref_subtitles_anidb_file OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.crossref_subtitles_anidb_file_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_subtitles_anidb_file_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.crossref_subtitles_anidb_file_seq OWNED BY {schema}.crossref_subtitles_anidb_file.crossref_subtitles_anidb_fileid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.customtag ( customtagid integer NOT NULL, tagname text, tagdescription text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.customtag OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.customtag_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.customtag_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.customtag_seq OWNED BY {schema}.customtag.customtagid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.duplicatefile ( duplicatefileid integer NOT NULL, filepathfile1 text, filepathfile2 text, importfolderidfile1 integer NOT NULL, importfolderidfile2 integer NOT NULL, hash character varying(50) NOT NULL, datetimeupdated timestamp with time zone NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.duplicatefile OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.duplicatefile_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.duplicatefile_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.duplicatefile_seq OWNED BY {schema}.duplicatefile.duplicatefileid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.filenamehash ( filenamehashid integer NOT NULL, filename text, filesize bigint NOT NULL, hash character varying(50) NOT NULL, datetimeupdated timestamp with time zone NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.filenamehash OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.filenamehash_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.filenamehash_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.filenamehash_seq OWNED BY {schema}.filenamehash.filenamehashid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.filterpreset ( filterpresetid integer NOT NULL, parentfilterpresetid integer, name text NOT NULL, filtertype integer NOT NULL, locked boolean NOT NULL, hidden boolean NOT NULL, applyatserieslevel boolean NOT NULL, expression text, sortingexpression text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.filterpreset OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.filterpreset_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.filterpreset_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.filterpreset_seq OWNED BY {schema}.filterpreset.filterpresetid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.ignoreanime ( ignoreanimeid integer NOT NULL, jmmuserid integer NOT NULL, animeid integer NOT NULL, ignoretype integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.ignoreanime OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.ignoreanime_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.ignoreanime_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.ignoreanime_seq OWNED BY {schema}.ignoreanime.ignoreanimeid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.importfolder ( importfolderid integer NOT NULL, importfoldertype integer NOT NULL, importfoldername character varying(500) DEFAULT NULL::character varying, importfolderlocation text, isdropsource integer NOT NULL, isdropdestination integer NOT NULL, iswatched integer NOT NULL, cloudid integer);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.importfolder OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.importfolder_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.importfolder_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.importfolder_seq OWNED BY {schema}.importfolder.importfolderid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.jmmuser ( jmmuserid integer NOT NULL, username character varying(100) DEFAULT NULL::character varying, password character varying(150) DEFAULT NULL::character varying, isadmin integer NOT NULL, isanidbuser integer NOT NULL, istraktuser integer NOT NULL, hidecategories text, caneditserversettings integer, plexusers text, plextoken text, avatarimageblob bytea, avatarimagemetadata character varying(128) DEFAULT NULL::character varying);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.jmmuser OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.jmmuser_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.jmmuser_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.jmmuser_seq OWNED BY {schema}.jmmuser.jmmuserid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.moviedb_fanart ( moviedb_fanartid integer NOT NULL, imageid character varying(100) DEFAULT NULL::character varying, movieid integer NOT NULL, imagetype character varying(100) DEFAULT NULL::character varying, imagesize character varying(100) DEFAULT NULL::character varying, url text, imagewidth integer NOT NULL, imageheight integer NOT NULL, enabled integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.moviedb_fanart OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.moviedb_fanart_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.moviedb_fanart_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.moviedb_fanart_seq OWNED BY {schema}.moviedb_fanart.moviedb_fanartid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.moviedb_movie ( moviedb_movieid integer NOT NULL, movieid integer NOT NULL, moviename character varying(250) DEFAULT NULL::character varying, originalname character varying(250) DEFAULT NULL::character varying, overview text, rating integer DEFAULT 0 NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.moviedb_movie OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.moviedb_movie_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.moviedb_movie_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.moviedb_movie_seq OWNED BY {schema}.moviedb_movie.moviedb_movieid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.moviedb_poster ( moviedb_posterid integer NOT NULL, imageid character varying(100) DEFAULT NULL::character varying, movieid integer NOT NULL, imagetype character varying(100) DEFAULT NULL::character varying, imagesize character varying(100) DEFAULT NULL::character varying, url text, imagewidth integer NOT NULL, imageheight integer NOT NULL, enabled integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.moviedb_poster OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.moviedb_poster_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.moviedb_poster_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.moviedb_poster_seq OWNED BY {schema}.moviedb_poster.moviedb_posterid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.playlist ( playlistid integer NOT NULL, playlistname text, playlistitems text, defaultplayorder integer NOT NULL, playwatched integer NOT NULL, playunwatched integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.playlist OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.playlist_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.playlist_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.playlist_seq OWNED BY {schema}.playlist.playlistid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.renamescript ( renamescriptid integer NOT NULL, scriptname text, script text, isenabledonimport integer NOT NULL, renamertype character varying(255) DEFAULT NULL::character varying, extradata text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.renamescript OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.renamescript_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.renamescript_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.renamescript_seq OWNED BY {schema}.renamescript.renamescriptid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.scan ( scanid integer NOT NULL, creationtime timestamp with time zone NOT NULL, importfolders text, status integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.scan OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.scan_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.scan_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.scan_seq OWNED BY {schema}.scan.scanid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.scanfile ( scanfileid integer NOT NULL, scanid integer NOT NULL, importfolderid integer NOT NULL, videolocal_place_id integer NOT NULL, fullname text, filesize bigint NOT NULL, status integer NOT NULL, checkdate timestamp with time zone, hash text, hashresult text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.scanfile OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.scanfile_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.scanfile_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.scanfile_seq OWNED BY {schema}.scanfile.scanfileid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.scheduledupdate ( scheduledupdateid integer NOT NULL, updatetype integer NOT NULL, lastupdate timestamp with time zone NOT NULL, updatedetails text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.scheduledupdate OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.scheduledupdate_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.scheduledupdate_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.scheduledupdate_seq OWNED BY {schema}.scheduledupdate.scheduledupdateid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.trakt_episode ( trakt_episodeid integer NOT NULL, trakt_showid integer NOT NULL, season integer NOT NULL, episodenumber integer NOT NULL, title character varying(500) DEFAULT NULL::character varying, url text, overview text, episodeimage character varying(500) DEFAULT NULL::character varying, traktid integer);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.trakt_episode OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.trakt_episode_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.trakt_episode_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.trakt_episode_seq OWNED BY {schema}.trakt_episode.trakt_episodeid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.trakt_season ( trakt_seasonid integer NOT NULL, trakt_showid integer NOT NULL, season integer NOT NULL, url text);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.trakt_season OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.trakt_season_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.trakt_season_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.trakt_season_seq OWNED BY {schema}.trakt_season.trakt_seasonid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.trakt_show ( trakt_showid integer NOT NULL, traktid character varying(100) DEFAULT NULL::character varying, title character varying(500) DEFAULT NULL::character varying, year character varying(50) DEFAULT NULL::character varying, url text, overview text, tvdb_id integer);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.trakt_show OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.trakt_show_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.trakt_show_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.trakt_show_seq OWNED BY {schema}.trakt_show.trakt_showid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.tvdb_episode ( tvdb_episodeid integer NOT NULL, id integer NOT NULL, seriesid integer NOT NULL, seasonid integer NOT NULL, seasonnumber integer NOT NULL, episodenumber integer NOT NULL, episodename text, overview text, filename text, epimgflag integer NOT NULL, absolutenumber integer, airsafterseason integer, airsbeforeepisode integer, airsbeforeseason integer, rating integer, airdate timestamp with time zone);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.tvdb_episode OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.tvdb_episode_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.tvdb_episode_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.tvdb_episode_seq OWNED BY {schema}.tvdb_episode.tvdb_episodeid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.tvdb_imagefanart ( tvdb_imagefanartid integer NOT NULL, id integer NOT NULL, seriesid integer NOT NULL, bannerpath character varying(200) DEFAULT NULL::character varying, bannertype character varying(200) DEFAULT NULL::character varying, bannertype2 character varying(200) DEFAULT NULL::character varying, colors character varying(200) DEFAULT NULL::character varying, language character varying(200) DEFAULT NULL::character varying, thumbnailpath character varying(200) DEFAULT NULL::character varying, vignettepath character varying(200) DEFAULT NULL::character varying, enabled integer NOT NULL, chosen integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.tvdb_imagefanart OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.tvdb_imagefanart_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.tvdb_imagefanart_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.tvdb_imagefanart_seq OWNED BY {schema}.tvdb_imagefanart.tvdb_imagefanartid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.tvdb_imageposter ( tvdb_imageposterid integer NOT NULL, id integer NOT NULL, seriesid integer NOT NULL, bannerpath character varying(200) DEFAULT NULL::character varying, bannertype character varying(200) DEFAULT NULL::character varying, bannertype2 character varying(200) DEFAULT NULL::character varying, language character varying(200) DEFAULT NULL::character varying, enabled integer NOT NULL, seasonnumber integer);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.tvdb_imageposter OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.tvdb_imageposter_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.tvdb_imageposter_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.tvdb_imageposter_seq OWNED BY {schema}.tvdb_imageposter.tvdb_imageposterid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.tvdb_imagewidebanner ( tvdb_imagewidebannerid integer NOT NULL, id integer NOT NULL, seriesid integer NOT NULL, bannerpath character varying(200) DEFAULT NULL::character varying, bannertype character varying(200) DEFAULT NULL::character varying, bannertype2 character varying(200) DEFAULT NULL::character varying, language character varying(200) DEFAULT NULL::character varying, enabled integer NOT NULL, seasonnumber integer);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.tvdb_imagewidebanner OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.tvdb_imagewidebanner_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.tvdb_imagewidebanner_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.tvdb_imagewidebanner_seq OWNED BY {schema}.tvdb_imagewidebanner.tvdb_imagewidebannerid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.tvdb_series ( tvdb_seriesid integer NOT NULL, seriesid integer NOT NULL, overview text, seriesname text, status character varying(100) DEFAULT NULL::character varying, banner character varying(100) DEFAULT NULL::character varying, fanart character varying(100) DEFAULT NULL::character varying, poster character varying(100) DEFAULT NULL::character varying, lastupdated character varying(100) DEFAULT NULL::character varying, rating integer);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.tvdb_series OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.tvdb_series_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.tvdb_series_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.tvdb_series_seq OWNED BY {schema}.tvdb_series.tvdb_seriesid;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.videolocal ( videolocalid integer NOT NULL, hash character varying(50) NOT NULL, crc32 character varying(50) DEFAULT NULL::character varying, md5 character varying(50) DEFAULT NULL::character varying, sha1 character varying(50) DEFAULT NULL::character varying, hashsource integer NOT NULL, filesize bigint NOT NULL, isignored integer NOT NULL, datetimeupdated timestamp with time zone NOT NULL, datetimecreated timestamp with time zone NOT NULL, isvariation integer NOT NULL, mediaversion integer DEFAULT 0 NOT NULL, mediablob bytea, filename text, mylistid integer DEFAULT 0 NOT NULL, datetimeimported timestamp with time zone, lastavdumped timestamp with time zone, lastavdumpversion character varying(128) DEFAULT NULL::character varying);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.videolocal OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.videolocal_place ( videolocal_place_id integer NOT NULL, videolocalid integer NOT NULL, filepath text, importfolderid integer NOT NULL, importfoldertype integer NOT NULL);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.videolocal_place OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.videolocal_place_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.videolocal_place_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.videolocal_place_seq OWNED BY {schema}.videolocal_place.videolocal_place_id;"),
        new DatabaseCommand(1, -1,"CREATE TABLE {schema}.videolocal_user ( videolocal_userid integer NOT NULL, jmmuserid integer NOT NULL, videolocalid integer NOT NULL, watcheddate timestamp with time zone, resumeposition bigint DEFAULT '0'::bigint NOT NULL, watchedcount integer DEFAULT 0 NOT NULL, lastupdated timestamp with time zone);"),
        new DatabaseCommand(1, -1,"ALTER TABLE {schema}.videolocal_user OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.videolocal_user_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.videolocal_user_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.videolocal_user_seq OWNED BY {schema}.videolocal_user.videolocal_userid;"),
        new DatabaseCommand(1, -1,"CREATE SEQUENCE {schema}.videolocal_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.videolocal_seq OWNER TO {owner};"),
        new DatabaseCommand(1, -1,"ALTER SEQUENCE {schema}.videolocal_seq OWNED BY {schema}.videolocal.videolocalid;"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime ALTER COLUMN anidb_animeid SET DEFAULT nextval('{schema}.anidb_anime_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_character ALTER COLUMN anidb_anime_characterid SET DEFAULT nextval('{schema}.anidb_anime_character_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_defaultimage ALTER COLUMN anidb_anime_defaultimageid SET DEFAULT nextval('{schema}.anidb_anime_defaultimage_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_relation ALTER COLUMN anidb_anime_relationid SET DEFAULT nextval('{schema}.anidb_anime_relation_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_similar ALTER COLUMN anidb_anime_similarid SET DEFAULT nextval('{schema}.anidb_anime_similar_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_staff ALTER COLUMN anidb_anime_staffid SET DEFAULT nextval('{schema}.anidb_anime_staff_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_tag ALTER COLUMN anidb_anime_tagid SET DEFAULT nextval('{schema}.anidb_anime_tag_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_title ALTER COLUMN anidb_anime_titleid SET DEFAULT nextval('{schema}.anidb_anime_title_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_animeupdate ALTER COLUMN anidb_animeupdateid SET DEFAULT nextval('{schema}.anidb_animeupdate_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_character ALTER COLUMN anidb_characterid SET DEFAULT nextval('{schema}.anidb_character_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_character_seiyuu ALTER COLUMN anidb_character_seiyuuid SET DEFAULT nextval('{schema}.anidb_character_seiyuu_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_episode ALTER COLUMN anidb_episodeid SET DEFAULT nextval('{schema}.anidb_episode_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_episode_title ALTER COLUMN anidb_episode_titleid SET DEFAULT nextval('{schema}.anidb_episode_title_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_file ALTER COLUMN anidb_fileid SET DEFAULT nextval('{schema}.anidb_file_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_fileupdate ALTER COLUMN anidb_fileupdateid SET DEFAULT nextval('{schema}.anidb_fileupdate_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_groupstatus ALTER COLUMN anidb_groupstatusid SET DEFAULT nextval('{schema}.anidb_groupstatus_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_recommendation ALTER COLUMN anidb_recommendationid SET DEFAULT nextval('{schema}.anidb_recommendation_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_releasegroup ALTER COLUMN anidb_releasegroupid SET DEFAULT nextval('{schema}.anidb_releasegroup_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_seiyuu ALTER COLUMN anidb_seiyuuid SET DEFAULT nextval('{schema}.anidb_seiyuu_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_tag ALTER COLUMN anidb_tagid SET DEFAULT nextval('{schema}.anidb_tag_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_vote ALTER COLUMN anidb_voteid SET DEFAULT nextval('{schema}.anidb_vote_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animecharacter ALTER COLUMN characterid SET DEFAULT nextval('{schema}.animecharacter_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animeepisode ALTER COLUMN animeepisodeid SET DEFAULT nextval('{schema}.animeepisode_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animeepisode_user ALTER COLUMN animeepisode_userid SET DEFAULT nextval('{schema}.animeepisode_user_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animegroup ALTER COLUMN animegroupid SET DEFAULT nextval('{schema}.animegroup_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animegroup_user ALTER COLUMN animegroup_userid SET DEFAULT nextval('{schema}.animegroup_user_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animeseries ALTER COLUMN animeseriesid SET DEFAULT nextval('{schema}.animeseries_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animeseries_user ALTER COLUMN animeseries_userid SET DEFAULT nextval('{schema}.animeseries_user_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animestaff ALTER COLUMN staffid SET DEFAULT nextval('{schema}.animestaff_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.authtokens ALTER COLUMN authid SET DEFAULT nextval('{schema}.authtokens_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.bookmarkedanime ALTER COLUMN bookmarkedanimeid SET DEFAULT nextval('{schema}.bookmarkedanime_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_mal ALTER COLUMN crossref_anidb_malid SET DEFAULT nextval('{schema}.crossref_anidb_mal_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_other ALTER COLUMN crossref_anidb_otherid SET DEFAULT nextval('{schema}.crossref_anidb_other_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_trakt_episode ALTER COLUMN crossref_anidb_trakt_episodeid SET DEFAULT nextval('{schema}.crossref_anidb_trakt_episode_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_traktv2 ALTER COLUMN crossref_anidb_traktv2id SET DEFAULT nextval('{schema}.crossref_anidb_traktv2_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_tvdb ALTER COLUMN crossref_anidb_tvdbid SET DEFAULT nextval('{schema}.crossref_anidb_tvdb_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_tvdb_episode ALTER COLUMN crossref_anidb_tvdb_episodeid SET DEFAULT nextval('{schema}.crossref_anidb_tvdb_episode_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_tvdb_episode_override ALTER COLUMN crossref_anidb_tvdb_episode_overrideid SET DEFAULT nextval('{schema}.crossref_anidb_tvdb_episode_override_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anime_staff ALTER COLUMN crossref_anime_staffid SET DEFAULT nextval('{schema}.crossref_anime_staff_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_customtag ALTER COLUMN crossref_customtagid SET DEFAULT nextval('{schema}.crossref_customtag_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_file_episode ALTER COLUMN crossref_file_episodeid SET DEFAULT nextval('{schema}.crossref_file_episode_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_languages_anidb_file ALTER COLUMN crossref_languages_anidb_fileid SET DEFAULT nextval('{schema}.crossref_languages_anidb_file_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_subtitles_anidb_file ALTER COLUMN crossref_subtitles_anidb_fileid SET DEFAULT nextval('{schema}.crossref_subtitles_anidb_file_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.customtag ALTER COLUMN customtagid SET DEFAULT nextval('{schema}.customtag_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.duplicatefile ALTER COLUMN duplicatefileid SET DEFAULT nextval('{schema}.duplicatefile_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.filenamehash ALTER COLUMN filenamehashid SET DEFAULT nextval('{schema}.filenamehash_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.filterpreset ALTER COLUMN filterpresetid SET DEFAULT nextval('{schema}.filterpreset_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.ignoreanime ALTER COLUMN ignoreanimeid SET DEFAULT nextval('{schema}.ignoreanime_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.importfolder ALTER COLUMN importfolderid SET DEFAULT nextval('{schema}.importfolder_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.jmmuser ALTER COLUMN jmmuserid SET DEFAULT nextval('{schema}.jmmuser_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.moviedb_fanart ALTER COLUMN moviedb_fanartid SET DEFAULT nextval('{schema}.moviedb_fanart_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.moviedb_movie ALTER COLUMN moviedb_movieid SET DEFAULT nextval('{schema}.moviedb_movie_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.moviedb_poster ALTER COLUMN moviedb_posterid SET DEFAULT nextval('{schema}.moviedb_poster_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.playlist ALTER COLUMN playlistid SET DEFAULT nextval('{schema}.playlist_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.renamescript ALTER COLUMN renamescriptid SET DEFAULT nextval('{schema}.renamescript_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.scan ALTER COLUMN scanid SET DEFAULT nextval('{schema}.scan_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.scanfile ALTER COLUMN scanfileid SET DEFAULT nextval('{schema}.scanfile_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.scheduledupdate ALTER COLUMN scheduledupdateid SET DEFAULT nextval('{schema}.scheduledupdate_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.trakt_episode ALTER COLUMN trakt_episodeid SET DEFAULT nextval('{schema}.trakt_episode_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.trakt_season ALTER COLUMN trakt_seasonid SET DEFAULT nextval('{schema}.trakt_season_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.trakt_show ALTER COLUMN trakt_showid SET DEFAULT nextval('{schema}.trakt_show_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.tvdb_episode ALTER COLUMN tvdb_episodeid SET DEFAULT nextval('{schema}.tvdb_episode_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.tvdb_imagefanart ALTER COLUMN tvdb_imagefanartid SET DEFAULT nextval('{schema}.tvdb_imagefanart_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.tvdb_imageposter ALTER COLUMN tvdb_imageposterid SET DEFAULT nextval('{schema}.tvdb_imageposter_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.tvdb_imagewidebanner ALTER COLUMN tvdb_imagewidebannerid SET DEFAULT nextval('{schema}.tvdb_imagewidebanner_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.tvdb_series ALTER COLUMN tvdb_seriesid SET DEFAULT nextval('{schema}.tvdb_series_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.videolocal ALTER COLUMN videolocalid SET DEFAULT nextval('{schema}.videolocal_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.videolocal_place ALTER COLUMN videolocal_place_id SET DEFAULT nextval('{schema}.videolocal_place_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.videolocal_user ALTER COLUMN videolocal_userid SET DEFAULT nextval('{schema}.videolocal_user_seq'::regclass);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime ADD CONSTRAINT pk_anidb_anime PRIMARY KEY (anidb_animeid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_animeupdate ADD CONSTRAINT pk_anidb_animeupdate PRIMARY KEY (anidb_animeupdateid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_character ADD CONSTRAINT pk_anidb_anime_character PRIMARY KEY (anidb_anime_characterid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_defaultimage ADD CONSTRAINT pk_anidb_anime_defaultimage PRIMARY KEY (anidb_anime_defaultimageid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_relation ADD CONSTRAINT pk_anidb_anime_relation PRIMARY KEY (anidb_anime_relationid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_similar ADD CONSTRAINT pk_anidb_anime_similar PRIMARY KEY (anidb_anime_similarid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_staff ADD CONSTRAINT pk_anidb_anime_staff PRIMARY KEY (anidb_anime_staffid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_tag ADD CONSTRAINT pk_anidb_anime_tag PRIMARY KEY (anidb_anime_tagid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_anime_title ADD CONSTRAINT pk_anidb_anime_title PRIMARY KEY (anidb_anime_titleid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_character ADD CONSTRAINT pk_anidb_character PRIMARY KEY (anidb_characterid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_character_seiyuu ADD CONSTRAINT pk_anidb_character_seiyuu PRIMARY KEY (anidb_character_seiyuuid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_episode ADD CONSTRAINT pk_anidb_episode PRIMARY KEY (anidb_episodeid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_episode_title ADD CONSTRAINT pk_anidb_episode_title PRIMARY KEY (anidb_episode_titleid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_file ADD CONSTRAINT pk_anidb_file PRIMARY KEY (anidb_fileid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_fileupdate ADD CONSTRAINT pk_anidb_fileupdate PRIMARY KEY (anidb_fileupdateid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_groupstatus ADD CONSTRAINT pk_anidb_groupstatus PRIMARY KEY (anidb_groupstatusid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_recommendation ADD CONSTRAINT pk_anidb_recommendation PRIMARY KEY (anidb_recommendationid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_releasegroup ADD CONSTRAINT pk_anidb_releasegroup PRIMARY KEY (anidb_releasegroupid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_seiyuu ADD CONSTRAINT pk_anidb_seiyuu PRIMARY KEY (anidb_seiyuuid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_tag ADD CONSTRAINT pk_anidb_tag PRIMARY KEY (anidb_tagid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.anidb_vote ADD CONSTRAINT pk_anidb_vote PRIMARY KEY (anidb_voteid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animecharacter ADD CONSTRAINT pk_animecharacter PRIMARY KEY (characterid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animeepisode ADD CONSTRAINT pk_animeepisode PRIMARY KEY (animeepisodeid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animeepisode_user ADD CONSTRAINT pk_animeepisode_user PRIMARY KEY (animeepisode_userid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animegroup ADD CONSTRAINT pk_animegroup PRIMARY KEY (animegroupid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animegroup_user ADD CONSTRAINT pk_animegroup_user PRIMARY KEY (animegroup_userid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animeseries ADD CONSTRAINT pk_animeseries PRIMARY KEY (animeseriesid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animeseries_user ADD CONSTRAINT pk_animeseries_user PRIMARY KEY (animeseries_userid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.animestaff ADD CONSTRAINT pk_animestaff PRIMARY KEY (staffid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.authtokens ADD CONSTRAINT pk_authtokens PRIMARY KEY (authid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.bookmarkedanime ADD CONSTRAINT pk_bookmarkedanime PRIMARY KEY (bookmarkedanimeid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_mal ADD CONSTRAINT pk_crossref_anidb_mal PRIMARY KEY (crossref_anidb_malid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_other ADD CONSTRAINT pk_crossref_anidb_other PRIMARY KEY (crossref_anidb_otherid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_traktv2 ADD CONSTRAINT pk_crossref_anidb_traktv2 PRIMARY KEY (crossref_anidb_traktv2id);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_trakt_episode ADD CONSTRAINT pk_crossref_anidb_trakt_episode PRIMARY KEY (crossref_anidb_trakt_episodeid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_tvdb ADD CONSTRAINT pk_crossref_anidb_tvdb PRIMARY KEY (crossref_anidb_tvdbid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_tvdb_episode ADD CONSTRAINT pk_crossref_anidb_tvdb_episode PRIMARY KEY (crossref_anidb_tvdb_episodeid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anidb_tvdb_episode_override ADD CONSTRAINT pk_crossref_anidb_tvdb_episode_override PRIMARY KEY (crossref_anidb_tvdb_episode_overrideid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_anime_staff ADD CONSTRAINT pk_crossref_anime_staff PRIMARY KEY (crossref_anime_staffid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_customtag ADD CONSTRAINT pk_crossref_customtag PRIMARY KEY (crossref_customtagid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_file_episode ADD CONSTRAINT pk_crossref_file_episode PRIMARY KEY (crossref_file_episodeid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_languages_anidb_file ADD CONSTRAINT pk_crossref_languages_anidb_file PRIMARY KEY (crossref_languages_anidb_fileid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.crossref_subtitles_anidb_file ADD CONSTRAINT pk_crossref_subtitles_anidb_file PRIMARY KEY (crossref_subtitles_anidb_fileid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.customtag ADD CONSTRAINT pk_customtag PRIMARY KEY (customtagid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.duplicatefile ADD CONSTRAINT pk_duplicatefile PRIMARY KEY (duplicatefileid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.filenamehash ADD CONSTRAINT pk_filenamehash PRIMARY KEY (filenamehashid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.filterpreset ADD CONSTRAINT pk_filterpreset PRIMARY KEY (filterpresetid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.ignoreanime ADD CONSTRAINT pk_ignoreanime PRIMARY KEY (ignoreanimeid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.importfolder ADD CONSTRAINT pk_importfolder PRIMARY KEY (importfolderid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.jmmuser ADD CONSTRAINT pk_jmmuser PRIMARY KEY (jmmuserid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.moviedb_fanart ADD CONSTRAINT pk_moviedb_fanart PRIMARY KEY (moviedb_fanartid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.moviedb_movie ADD CONSTRAINT pk_moviedb_movie PRIMARY KEY (moviedb_movieid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.moviedb_poster ADD CONSTRAINT pk_moviedb_poster PRIMARY KEY (moviedb_posterid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.playlist ADD CONSTRAINT pk_playlist PRIMARY KEY (playlistid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.renamescript ADD CONSTRAINT pk_renamescript PRIMARY KEY (renamescriptid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.scan ADD CONSTRAINT pk_scan PRIMARY KEY (scanid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.scanfile ADD CONSTRAINT pk_scanfile PRIMARY KEY (scanfileid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.scheduledupdate ADD CONSTRAINT pk_scheduledupdate PRIMARY KEY (scheduledupdateid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.trakt_episode ADD CONSTRAINT pk_trakt_episode PRIMARY KEY (trakt_episodeid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.trakt_season ADD CONSTRAINT pk_trakt_season PRIMARY KEY (trakt_seasonid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.trakt_show ADD CONSTRAINT pk_trakt_show PRIMARY KEY (trakt_showid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.tvdb_episode ADD CONSTRAINT pk_tvdb_episode PRIMARY KEY (tvdb_episodeid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.tvdb_imagefanart ADD CONSTRAINT pk_tvdb_imagefanart PRIMARY KEY (tvdb_imagefanartid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.tvdb_imageposter ADD CONSTRAINT pk_tvdb_imageposter PRIMARY KEY (tvdb_imageposterid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.tvdb_imagewidebanner ADD CONSTRAINT pk_tvdb_imagewidebanner PRIMARY KEY (tvdb_imagewidebannerid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.tvdb_series ADD CONSTRAINT pk_tvdb_series PRIMARY KEY (tvdb_seriesid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.videolocal ADD CONSTRAINT pk_videolocal PRIMARY KEY (videolocalid);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.videolocal_place ADD CONSTRAINT pk_videolocal_place PRIMARY KEY (videolocal_place_id);"),
        new DatabaseCommand(1, -1,"ALTER TABLE ONLY {schema}.videolocal_user ADD CONSTRAINT pk_videolocal_user PRIMARY KEY (videolocal_userid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_anime_animeid ON {schema}.anidb_anime USING btree (animeid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX uix_anidb_animeupdate ON {schema}.anidb_animeupdate USING btree (animeid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_anime_character_animeid ON {schema}.anidb_anime_character USING btree (animeid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_anime_character_charid ON {schema}.anidb_anime_character USING btree (charid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_anime_character_animeid_charid ON {schema}.anidb_anime_character USING btree (animeid, charid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_anime_defaultimage_imagetype ON {schema}.anidb_anime_defaultimage USING btree (animeid, imagetype);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_anime_relation_animeid ON {schema}.anidb_anime_relation USING btree (animeid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_anime_relation_animeid_relatedanimeid ON {schema}.anidb_anime_relation USING btree (animeid, relatedanimeid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_anime_similar_animeid ON {schema}.anidb_anime_similar USING btree (animeid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_anime_similar_animeid_similaranimeid ON {schema}.anidb_anime_similar USING btree (animeid, similaranimeid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_anime_tag_animeid ON {schema}.anidb_anime_tag USING btree (animeid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_anime_tag_animeid_tagid ON {schema}.anidb_anime_tag USING btree (animeid, tagid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_anime_title_animeid ON {schema}.anidb_anime_title USING btree (animeid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_character_charid ON {schema}.anidb_character USING btree (charid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_character_seiyuu_charid ON {schema}.anidb_character_seiyuu USING btree (charid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_character_seiyuu_seiyuuid ON {schema}.anidb_character_seiyuu USING btree (seiyuuid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_character_seiyuu_charid_seiyuuid ON {schema}.anidb_character_seiyuu USING btree (charid, seiyuuid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_episode_animeid ON {schema}.anidb_episode USING btree (animeid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_episode_episodetype ON {schema}.anidb_episode USING btree (episodetype);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_episode_episodeid ON {schema}.anidb_episode USING btree (episodeid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_file_fileid ON {schema}.anidb_file USING btree (fileid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_file_hash ON {schema}.anidb_file USING btree (hash);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_fileupdate ON {schema}.anidb_fileupdate USING btree (filesize, hash);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_anidb_groupstatus_animeid ON {schema}.anidb_groupstatus USING btree (animeid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_groupstatus_animeid_groupid ON {schema}.anidb_groupstatus USING btree (animeid, groupid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_recommendation ON {schema}.anidb_recommendation USING btree (animeid, userid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_releasegroup_groupid ON {schema}.anidb_releasegroup USING btree (groupid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_seiyuu_seiyuuid ON {schema}.anidb_seiyuu USING btree (seiyuuid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_tag_tagid ON {schema}.anidb_tag USING btree (tagid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_animeepisode_animeseriesid ON {schema}.animeepisode USING btree (animeseriesid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_animeepisode_anidb_episodeid ON {schema}.animeepisode USING btree (anidb_episodeid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_animeepisode_user_user_animeseriesid ON {schema}.animeepisode_user USING btree (jmmuserid, animeseriesid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_animeepisode_user_user_episodeid ON {schema}.animeepisode_user USING btree (jmmuserid, animeepisodeid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_animegroup_user_user_groupid ON {schema}.animegroup_user USING btree (jmmuserid, animegroupid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_animeseries_anidb_id ON {schema}.animeseries USING btree (anidb_id);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_animeseries_user_user_seriesid ON {schema}.animeseries_user USING btree (jmmuserid, animeseriesid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_bookmarkedanime_animeid ON {schema}.bookmarkedanime USING btree (animeid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_crossref_anidb_other ON {schema}.crossref_anidb_other USING btree (animeid, crossrefid, crossrefsource, crossreftype);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_crossref_anidb_traktv2 ON {schema}.crossref_anidb_traktv2 USING btree (animeid, traktseasonnumber, traktstartepisodenumber, anidbstartepisodetype, anidbstartepisodenumber);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_crossref_anidb_trakt_episode_anidbepisodeid ON {schema}.crossref_anidb_trakt_episode USING btree (anidbepisodeid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_tvdb_anidbid_tvdbid ON {schema}.crossref_anidb_tvdb USING btree (anidbid, tvdbid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_crossref_anidb_tvdb_episode_anidbid_tvdbid ON {schema}.crossref_anidb_tvdb_episode USING btree (anidbepisodeid, tvdbepisodeid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_anidb_tvdb_episode_override_anidbepisodeid_tvdbepisodeid ON {schema}.crossref_anidb_tvdb_episode_override USING btree (anidbepisodeid, tvdbepisodeid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_crossref_file_episode_episodeid ON {schema}.crossref_file_episode USING btree (episodeid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_crossref_file_episode_hash_episodeid ON {schema}.crossref_file_episode USING btree (hash, episodeid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_filterpreset_filtertype ON {schema}.filterpreset USING btree (filtertype);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_filterpreset_lockedhidden ON {schema}.filterpreset USING btree (locked, hidden);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_filterpreset_name ON {schema}.filterpreset USING btree (name);"),
        new DatabaseCommand(1, -1,"CREATE INDEX ix_filterpreset_parentfilterpresetid ON {schema}.filterpreset USING btree (parentfilterpresetid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_ignoreanime_user_animeid ON {schema}.ignoreanime USING btree (jmmuserid, animeid, ignoretype);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_moviedb_movie_id ON {schema}.moviedb_movie USING btree (movieid);"),
        new DatabaseCommand(1, -1,"CREATE INDEX uix_scanfilestatus ON {schema}.scanfile USING btree (scanid, status, checkdate);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_scheduledupdate_updatetype ON {schema}.scheduledupdate USING btree (updatetype);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_tvdb_episode_id ON {schema}.tvdb_episode USING btree (id);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_tvdb_imagefanart_id ON {schema}.tvdb_imagefanart USING btree (id);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_tvdb_imageposter_id ON {schema}.tvdb_imageposter USING btree (id);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_tvdb_imagewidebanner_id ON {schema}.tvdb_imagewidebanner USING btree (id);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_tvdb_series_id ON {schema}.tvdb_series USING btree (seriesid);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_videolocal_hash ON {schema}.videolocal USING btree (hash);"),
        new DatabaseCommand(1, -1,"CREATE UNIQUE INDEX uix_videolocal_user_user_videolocalid ON {schema}.videolocal_user USING btree (jmmuserid, videolocalid);"),
        new DatabaseCommand(1, -1,"CREATE TRIGGER on_update_current_timestamp BEFORE UPDATE ON {schema}.videolocal_user FOR EACH ROW EXECUTE FUNCTION {schema}.on_update_current_timestamp_videolocal_user();")
    };

    private List<DatabaseCommand> patchCommands = new()
    {
    };

    public override void BackupDatabase(string fullfilename)
    {

        //Not Supported yet
    }

    public override bool TestConnection()
    {
        try
        {
            using (var conn = new NpgsqlConnection(GetTestConnectionString()))
            {
                var query = "select 1";
                var cmd = new NpgsqlCommand(query, conn);
                conn.Open();
                cmd.ExecuteScalar();
                return true;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    protected override Tuple<bool, string> ExecuteCommand(NpgsqlConnection connection, string command)
    {
        try
        {
            Execute(connection, command);
            return new Tuple<bool, string>(true, null);
        }
        catch (Exception ex)
        {
            return new Tuple<bool, string>(false, ex.ToString());
        }
    }
    public class SequenceNpgsqlDriver : NpgsqlDriver
    {
        public override DbCommand GenerateCommand(CommandType type, SqlString sqlString, SqlType[] parameterTypes)
        {
            DbCommand command = base.GenerateCommand(type, sqlString, parameterTypes);
            if (command.CommandText.Contains("hibernate_sequence"))
            {
                string table = GetTableFromCommand(command.CommandText);
                if (table != null)
                {
                    table = ""+table.Trim('"') + "_seq";
                    command.CommandText = command.CommandText.Replace("hibernate_sequence", table);

                }
            }
            return command;
        }

        private string GetTableFromCommand(string s)
        {
            if (s.StartsWith("INSERT INTO "))
            {
                int a = s.IndexOf(" ", 13);
                return s.Substring(12, a - 12).Trim();
            }

            return null;
        }
    }
    protected override void Execute(NpgsqlConnection connection, string command)
    {
        using (var scommand = new NpgsqlCommand(command, connection))
        {
            scommand.ExecuteNonQuery();
        }
    }

    protected override long ExecuteScalar(NpgsqlConnection connection, string command)
    {
        using (var cmd = new NpgsqlCommand(command, connection))
        {
            var result = cmd.ExecuteScalar();
            return long.Parse(result.ToString());
        }
    }

    protected override List<object[]> ExecuteReader(NpgsqlConnection connection, string command)
    {
        using var cmd = new NpgsqlCommand(command, connection);

        using var reader = cmd.ExecuteReader();
        var rows = new List<object[]>();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(values);
        }

        reader.Close();
        return rows;
    }

    protected override void ConnectionWrapper(string connectionstring, Action<NpgsqlConnection> action)
    {
        using var conn = new NpgsqlConnection(connectionstring);
        conn.Open();
        action(conn);
    }

    public override string GetConnectionString()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        return
            $"Server={settings.Database.Hostname};Port={settings.Database.Port};Database={settings.Database.Schema.ToLowerInvariant()};User ID={settings.Database.Username};Password={settings.Database.Password};CommandTimeout=3600";
    }

    public override string GetTestConnectionString()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        return
            $"Server={settings.Database.Hostname};Port={settings.Database.Port};User ID={settings.Database.Username};Password={settings.Database.Password};CommandTimeout=3600;";
    }

    public override bool HasVersionsTable()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var connStr = GetTestConnectionString();
        string sql = $"SELECT COUNT(*) FROM pg_database WHERE datname = '{settings.Database.Schema}.ToLowerInvariant()';";
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();
        long result = ExecuteScalar(conn, sql);
        if (result == 0)
        {
            return false;
        }
        connStr = GetConnectionString();
        sql = $"SELECT COUNT(*) FROM pg_tables WHERE schema= '{settings.Database.Schema}.ToLowerInvariant()' AND tablename  = 'versions';";
        using var conn2 = new NpgsqlConnection(connStr);
        var com = new NpgsqlCommand(sql, conn);
        conn2.Open();
        var count = (long)com.ExecuteScalar();
        return count > 0;
    }

    public override ISessionFactory CreateSessionFactory()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        return Fluently.Configure(new Configuration().SetNamingStrategy(new LowercaseNamingStrategy(settings.Database.Schema)))
            .Database(FluentNHibernate.Cfg.Db.PostgreSQLConfiguration.Standard.ConnectionString(x => x.Database(settings.Database.Schema.ToLowerInvariant())
                    .Host(settings.Database.Hostname)
                    .Port(settings.Database.Port)
                    .Username(settings.Database.Username)
                    .Password(settings.Database.Password))
                .Driver<SequenceNpgsqlDriver>())
            .Mappings(m =>
            {
                m.FluentMappings.AddFromAssemblyOf<ShokoServer>();
            })
            .ExposeConfiguration(c => c.DataBaseIntegration(prop =>
            {
                // uncomment this for SQL output
                //prop.LogSqlInConsole = true;
            }).SetInterceptor(new NHibernateDependencyInjector(Utils.ServiceContainer))).BuildSessionFactory();
    }
    public class LowercaseNamingStrategy : INamingStrategy
    {
        public string Schema { get; set; }

        public LowercaseNamingStrategy(string schema)
        {
            Schema = schema.ToLowerInvariant();
        }
        public string ClassToTableName(string className)
        {
            className = className.Trim('`');
            return $"{Schema}.{className.ToLowerInvariant()}";
        }

        public string PropertyToColumnName(string propertyName)
        {
            return $"{propertyName.ToLowerInvariant()}";
        }

        public string TableName(string tableName)
        {
            tableName = tableName.Trim('`');
            return $"{Schema}.{tableName.ToLowerInvariant()}";
        }

        public string ColumnName(string columnName)
        {
            return $"{columnName.ToLowerInvariant()}";
        }

        public string PropertyToTableName(string className, string propertyName)
        {
            className = className.Trim('`');
            return $"{Schema}.{className.ToLowerInvariant()}_{propertyName.ToLowerInvariant()}";
        }

        public string LogicalColumnName(string columnName, string propertyName)
        {
            return $"{columnName.ToLowerInvariant()}_{propertyName.ToLowerInvariant()}";
        }
    }
    public override bool DatabaseAlreadyExists()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        try
        {
            var connStr = GetTestConnectionString();

            var sql = $"SELECT COUNT(*) FROM pg_database WHERE datname = '{settings.Database.Schema.ToLowerInvariant()}';";
            Logger.Trace(sql);

            using var conn = new NpgsqlConnection(connStr);
            conn.Open();
            long result = ExecuteScalar(conn, sql);
            if (result>0)
            {
                Logger.Trace("Found db already exists: {DB}", settings.Database.Schema);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, ex.ToString());
        }

        Logger.Trace("db does not exist: {0}", settings.Database.Schema);
        return false;
    }


    public override void CreateDatabase()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        try
        {
            if (DatabaseAlreadyExists())
            {
                return;
            }

            var connStr = GetTestConnectionString();
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();
            ServerState.Instance.ServerStartingStatus = "Database - Creating Initial Schema...";
            ExecuteWithException(conn, AddSchemaAndOwner(settings, createDatabase, true));
            connStr = GetConnectionString();
            using var conn2 = new NpgsqlConnection(connStr);
            conn2.Open();
            ExecuteWithException(conn2, AddSchemaAndOwner(settings, createSchema));

        }
        catch (Exception ex)
        {
            Logger.Error(ex, ex.ToString());
        }
    }

    private DatabaseCommand AddSchemaAndOwner(IServerSettings settings, DatabaseCommand cmd, bool withoutSetup=false)
    {
        return new DatabaseCommand(cmd.Version, cmd.Revision, (withoutSetup ? "" : setup) + cmd.Command.Replace("{schema}", settings.Database.Schema.ToLowerInvariant()).Replace("{owner}", settings.Database.Username));
    }

    private List<DatabaseCommand> AddSchemaAndOwner(IServerSettings settings, IEnumerable<DatabaseCommand> cmds, bool withoutSetup = false)
    {
        List<DatabaseCommand> cs = cmds.Select(a => AddSchemaAndOwner(settings, a, withoutSetup)).ToList();
        int increment = 0;
        for(int x = 0; x < cs.Count; x++)
        {
            if (cs[x].Revision == -1)
            {
                cs[x] = new DatabaseCommand(cs[x].Version, increment, cs[x].Command);
                increment++;
            }
            else
                increment= cs[x].Revision + 1;
        }
        return cs;
    }
    public override void CreateAndUpdateSchema()
    {
        ConnectionWrapper(GetConnectionString(), myConn =>
        {
            var settings = Utils.SettingsProvider.GetSettings();
            var create = false;
            var count = ExecuteScalar(myConn,$"SELECT COUNT(*) FROM pg_tables WHERE schemaname= '{settings.Database.Schema.ToLowerInvariant()}' AND tablename  = 'versions';");
            if (count == 0)
                create = true;

            if (create)
            {
                ExecuteWithException(myConn, AddSchemaAndOwner(settings, createVersionTable));

                List<DatabaseCommand> commands = AddSchemaAndOwner(settings, createDatabase, true);
                commands.AddRange(AddSchemaAndOwner(settings, createSchema));
                commands.AddRange(AddSchemaAndOwner(settings, createVersionTable));
                foreach (DatabaseCommand cmd in commands)
                {
                    AddVersion(cmd.Version.ToString(), cmd.Revision.ToString(), cmd.CommandName);
                }

                PreFillVersions(commands);
                ExecuteWithException(myConn, AddSchemaAndOwner(settings, createTables));
            }


            ServerState.Instance.ServerStartingStatus = "Database - Applying Schema Patches...";

            ExecuteWithException(myConn, AddSchemaAndOwner(settings, patchCommands));
        });
    }


}
