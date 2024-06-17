using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Server.API.Annotations;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using System.Collections.Generic;
using System.Linq;

using ApiRenamer = Shoko.Server.API.v3.Models.Shoko.Renamer;
using RenameFileHelper = Shoko.Server.Renamer.RenameFileHelper;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class RenamerController : BaseController
{
    public RenamerController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }

    /// <summary>
    /// Get a list of all <see cref="ApiRenamer"/>s.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<List<ApiRenamer>> GetAllRenamers()
    {
        return RenameFileHelper.Renamers
            .Select(p => new ApiRenamer(p.Key, p.Value))
            .ToList();
    }

    /// <summary>
    /// Get the <see cref="ApiRenamer"/> by the given <paramref name="renamerName"/>.
    /// </summary>
    /// <param name="renamerName">Renamer ID</param>
    /// <returns></returns>
    [HttpGet("{renamerName}")]
    public ActionResult<ApiRenamer> GetRenamer([FromRoute] string renamerName)
    {
        if (!RenameFileHelper.Renamers.TryGetValue(renamerName, out var value))
            return NotFound("Renamer not found.");

        return new ApiRenamer(renamerName, value);
    }

    /// <summary>
    /// Modifies the settings of the <see cref="ApiRenamer"/> with the
    /// given /// <paramref name="renamerName"/>.
    /// </summary>
    /// <param name="renamerName">
    /// The name of the renamer to be updated.
    /// </param>
    /// <param name="body">
    /// An object containing the modifications to be applied to the renamer.
    /// </param>
    /// <returns>
    /// The modified renamer if the operation is successful, or an error
    /// response if the renamer is not found or the modification fails.
    /// </returns>
    [Authorize("admin")]
    [HttpPut("{renamerName}")]
    public ActionResult<ApiRenamer> PutRenamer([FromRoute] string renamerName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ApiRenamer.Input.ModifyRenamerBody body)
    {
        if (!RenameFileHelper.Renamers.TryGetValue(renamerName, out var value))
            return NotFound("Renamer not found.");

        return body.MergeWithExisting(renamerName, value);
    }

    /// <summary>
    /// Applies a JSON patch document to modify the settings of the
    /// <see cref="ApiRenamer"/> with the given
    /// <paramref name="renamerName"/>.
    /// </summary>
    /// <param name="renamerName">
    /// The name of the renamer to be patched.
    /// </param>
    /// <param name="patchDocument">
    /// A JSON Patch document containing the modifications to be applied to the
    /// renamer.
    /// </param>
    /// <returns>
    /// The modified renamer if the operation is successful, or an error
    /// response if the renamer is not found, the patch document is invalid, or
    /// the modifications fail.
    /// </returns>
    [Authorize("admin")]
    [HttpPatch("{renamerName}")]
    public ActionResult<ApiRenamer> PatchRenamer([FromRoute] string renamerName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<ApiRenamer.Input.ModifyRenamerBody> patchDocument)
    {
        if (!RenameFileHelper.Renamers.TryGetValue(renamerName, out var value))
            return NotFound("Renamer not found.");

        // Patch the renamer in the v3 model and merge it back into the
        // settings.
        var modifyRenamer = new ApiRenamer.Input.ModifyRenamerBody(renamerName);
        patchDocument.ApplyTo(modifyRenamer, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return modifyRenamer.MergeWithExisting(renamerName, value);
    }

    /// <summary>
    /// Get the <see cref="ApiRenamer.Script"/>s for all or a single renamer.
    /// </summary>
    /// <param name="renamerName">Renamer ID</param>
    /// <returns>The scripts.</returns>
    [HttpGet("Script")]
    public ActionResult<List<ApiRenamer.Script>> GetAllRenamerScripts([FromQuery] string renamerName = null)
    {
        if (!string.IsNullOrEmpty(renamerName))
        {
            if (!RenameFileHelper.Renamers.ContainsKey(renamerName))
                return new List<ApiRenamer.Script>();
            var renamer = RenameFileHelper.Renamers[renamerName];

            return RepoFactory.RenameScript.GetByRenamerType(renamerName)
                .Where(s => s.ScriptName != Shoko.Models.Constants.Renamer.TempFileName)
                .Select(s => new ApiRenamer.Script(s))
                .ToList();
        }

        return RepoFactory.RenameScript.GetAll()
            .Where(s => s.ScriptName != Shoko.Models.Constants.Renamer.TempFileName)
            .Select(s => new ApiRenamer.Script(s))
            .ToList();
    }

    /// <summary>
    /// Add a new script.
    /// </summary>
    /// <param name="body">The script to add.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Script")]
    public ActionResult<ApiRenamer.Script> AddRenamerScript([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ApiRenamer.Input.NewScriptBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("Script name cannot be empty.");

        var script = RepoFactory.RenameScript.GetByName(body.Name);
        if (script != null)
            return BadRequest("A script with the given name already exists!");

        script = new Shoko.Models.Server.RenameScript
        {
            ScriptName = body.Name,
            RenamerType = body.RenamerName,
            IsEnabledOnImport = body.EnabledOnImport ? 1 : 0,
            Script = body.Body,
            ExtraData = null,
        };
        RepoFactory.RenameScript.Save(script);

        return Created($"/api/v3/Renamer/Script/{script.RenameScriptID}", new ApiRenamer.Script(script));
    }

    /// <summary>
    /// Get a <see cref="ApiRenamer.Script"/> by the given <paramref name="scriptID"/>.
    /// </summary>
    /// <param name="scriptID">Script ID</param>
    /// <returns>The script</returns>
    [HttpGet("Script/{scriptID}")]
    public ActionResult<ApiRenamer.Script> GetRenamerScriptByScriptID([FromRoute] int scriptID)
    {
        var script = RepoFactory.RenameScript.GetByID(scriptID);
        if (script == null)
            return NotFound("Renamer.Script not found.");

        return new ApiRenamer.Script(script);
    }

    /// <summary>
    /// Replace an existing <see cref="ApiRenamer.Script"/> by the given <paramref name="scriptID"/>.
    /// </summary>
    /// <param name="scriptID">Script ID</param>
    /// <param name="modifyScript">The modified script to replace the existing script with.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("Script/{scriptID}")]
    public ActionResult<ApiRenamer.Script> PutRenamerScriptByScriptID([FromRoute] int scriptID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ApiRenamer.Input.ModifyScriptBody modifyScript)
    {
        var script = RepoFactory.RenameScript.GetByID(scriptID);
        if (script == null)
            return NotFound("Renamer.Script not found.");

        if (string.IsNullOrWhiteSpace(modifyScript.Name))
            return BadRequest("Script name cannot be empty.");

        // Guard against rename-collisions.
        if (modifyScript.Name != script.ScriptName)
        {
            var anotherScript = RepoFactory.RenameScript.GetByName(modifyScript.Name);
            if (anotherScript != null)
                return BadRequest("Another script with the given name already exists!");
        }

        return modifyScript.MergeWithExisting(script);
    }

    /// <summary>
    /// Patch an existing <see cref="ApiRenamer.Script"/> by the given
    /// <paramref name="scriptID"/>.
    /// </summary>
    /// <param name="scriptID">Script ID</param>
    /// <param name="patchDocument">Thejson patch document to update the script
    /// with.</param>
    /// <returns>The updated <see cref="ApiRenamer.Script"/></returns>
    [Authorize("admin")]
    [HttpPatch("Script/{scriptID}")]
    public ActionResult<ApiRenamer.Script> PatchRenamerScriptByScriptID([FromRoute] int scriptID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<ApiRenamer.Input.ModifyScriptBody> patchDocument)
    {
        var script = RepoFactory.RenameScript.GetByID(scriptID);
        if (script == null)
            return NotFound("Renamer.Script not found.");

        // Patch the script in the v3 model and merge it back into the database
        // model.
        var modifyScript = new ApiRenamer.Input.ModifyScriptBody(script);
        patchDocument.ApplyTo(modifyScript, ModelState);
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(modifyScript.Name))
            return BadRequest("Script name cannot be empty.");

        // Guard against rename-collisions.
        if (modifyScript.Name != script.ScriptName)
        {
            var anotherScript = RepoFactory.RenameScript.GetByName(modifyScript.Name);
            if (anotherScript != null)
                return BadRequest("Another script with the given name already exists!");
        }

        return modifyScript.MergeWithExisting(script);
    }

    /// <summary>
    /// Delete an existing <see cref="ApiRenamer.Script"/> by the given <paramref name="scriptID"/>
    /// </summary>
    /// <param name="scriptID">Script ID</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Script/{scriptID}")]
    public ActionResult DeleteRenamerScriptByScriptID([FromRoute] int scriptID)
    {
        var script = RepoFactory.RenameScript.GetByID(scriptID);
        if (script == null)
            return NotFound("Renamer.Script not found.");

        RepoFactory.RenameScript.Delete(script);

        return NoContent();
    }
}
