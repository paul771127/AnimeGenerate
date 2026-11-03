for(const job of jobs){
const actor=load("forestActors").find(a=>a[0]===job.id),[id,zh,design,attack]=actor,root=load("artRoot"),dir="spellduel-unity/Tools/sprite_enemies/forest_boss_sources/forest_"+id;
let prompt,refs,rows;
const style="Detailed Japanese manga dark fantasy JRPG monster illustration, fine variable-weight ink lines, cel-shaded shadows plus subtle gradients and highlights, consistent upper-left light. Slightly stylized semi-chibi proportions: humanoids about 4.5 to 5 heads tall, mature faces, slightly larger head and hands, compact limbs; creatures have heads about 12% larger than natural with compact expressive silhouette. NOT tiny 2.5-head chibi, not baby-like, no cute face, no smile. Corrupted ancient forest palette: dark/moss green, bark brown, poison purple; yellow-green or teal emissive details. Entire character and appendages visible. Real transparent background alpha=0, absolutely no paper/backdrop/ground shadow or broad ambient glow.";
if(job.group==="master"){
prompt="Design ONE full-body game enemy: "+design+" "+style+" Threatening three-quarter front view facing RIGHT, compact readable silhouette and generous 15% empty margin. Reference image is STYLE ONLY; DO NOT COPY its pumpkin, body, clothing or equipment. Follow the new creature description exactly.";
refs=[root+"/spellduel-unity/Tools/sprite_enemies/ff_sources/pumpkin/master.png"];
}else{
const desc={idle:[4,"Four subtle breathing idle keyframes; natural sway, SAME scale."],walk:[6,"Six DISTINCT locomotion cycle phases, alternating footsteps with bent joints, plant root steps or natural quadruped trot appropriate to anatomy. No extra limbs."],combat:[5,"Rows 1 and 2 are WINDUP start and full charge; rows 3,4,5 are attack release, followthrough, recovery. "+attack],reaction:[6,"Rows1,2: HIT recoil back then stagger. Rows3,4,5,6: DYING knees/roots buckle, collapse sideways, disintegrating fallen body, FINAL LOW FLAT REMAINS under half standing height. Never enlarge fallen body."]};
[rows,prompt]=desc[job.group];
prompt="Production animation sheet for EXACT character in reference. EIGHT equally spaced columns and "+rows+" rows, "+rows*8+" full-body drawings. Wide landscape sheet, maximum resolution. Column directions FIXED in every row: 1 FRONT; 2 FRONT RIGHT; 3 RIGHT PROFILE; 4 BACK RIGHT; 5 BACK; 6 BACK LEFT; 7 LEFT PROFILE; 8 FRONT LEFT. Columns6,7,8 noses point SCREEN LEFT. Back shows back, no face. Same creature identity, anatomy, colors and equipment in ALL views, never mirror handedness. "+prompt+" "+design+" "+style+" Identical camera scale across every frame including collapsed ones. All feet/tails/weapons fit INSIDE each cell with at least 12% empty gutters on ALL sides. No grid or labels. NO overlapping neighbors; no clipping. Return a clean RGBA sprite sheet.";
refs=[root+"/"+dir+"/master.png"];
}
const r=await tools.image_gen__imagegen({prompt,referenced_image_paths:refs,transparent_background:true});generatedImage(r);
const src=r.output_hint.match(/as (C:\\[^\n]+?\.png) by default/)[1].replaceAll("\\","/");
const meta=JSON.stringify({id:"forest_"+id,name:zh,group:job.group,columns:job.group==="master"?1:8,rows:rows||1,prompt},null,2);
text(await tools.exec_command({cmd:"New-Item -ItemType Directory -Force '"+dir+"' | Out-Null\nCopy-Item -LiteralPath '"+src+"' -Destination '"+dir+"/"+job.group+".png'\n@'\n"+meta+"\n'@ | Set-Content -Encoding utf8 '"+dir+"/"+job.group+".json'",workdir:root,max_output_tokens:100}));
}
