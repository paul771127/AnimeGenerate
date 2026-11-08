import unittest,pathlib,importlib.util,tempfile,struct,math
api_path=pathlib.Path(__file__).resolve().parents[2]/'town_kit_control.py'
spec=importlib.util.spec_from_file_location('town007_api',str(api_path));api=importlib.util.module_from_spec(spec);spec.loader.exec_module(api)
from town_spec import plan,inside,clearance,transform,shell
class TownContract(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.layout,cls.report=plan()
    def test_required_functional_and_story_roles(self):
        self.assertEqual({n['role']for n in self.layout['npcs']},{'smith','shop','guild','dungeon','training','bounty','watch','story:老焰','story:小鐵','story:米菈'})
        indoors={n['role']:n['inside']for n in self.layout['npcs']}
        for role,room in [('smith','smith'),('story:小鐵','smith'),('shop','shop'),('guild','guild'),('story:老焰','house_chief'),('dungeon','dungeon')]:self.assertEqual(indoors[role],room)
        for role in ['training','bounty','watch','story:米菈']:self.assertIsNone(indoors[role])
    def test_map_contract_and_facility_displacement_limits(self):
        l=self.layout;self.assertEqual(l['plaza'],[0,5]);self.assertEqual(l['spawn'],[0,0]);self.assertEqual(l['radius'],13);self.assertEqual(l['exits'][0]['pos'],[13,5]);self.assertEqual(l['exits'][0]['halfWidth'],1.4)
        expected={'smith':[-7,8],'shop':[7,8],'guild':[0,13.5],'dungeon':[-9.5,1.5],'training':[9.5,1.5],'bounty':[-5.5,-5],'watch':[5.5,-5],'landmark':[-8,12]}
        for b in l['buildings']:
            if b['id']in expected:self.assertTrue(all(abs(a-z)<=1.50001 for a,z in zip(b['pos'],expected[b['id']])))
    def test_room_clearance_is_not_floor_area_only(self):
        for b in self.layout['buildings']:
            if b['enterable']:
                self.assertGreaterEqual(min(b['clearWalkSize']),4);self.assertGreaterEqual(b['clearWalkArea'],15.9999);self.assertGreaterEqual(b['ceilingHeight'],3);self.assertGreaterEqual(b['door']['width'],1.8)
                self.assertLessEqual(b['walkableArea'],b['floorArea']);self.assertGreaterEqual(b['walkableArea'],16)
    def test_npcs_face_accessible_conversation_positions(self):
        for n in self.layout['npcs']:
            p=n['conversation']['playerPos'];a=math.radians(n['yaw']);forward=(p[0]-n['pos'][0])*math.sin(a)+(p[1]-n['pos'][1])*math.cos(a)
            self.assertGreater(forward,1.2);self.assertGreater(clearance(p,self.layout['colliders']),.3);self.assertGreaterEqual(n['conversation']['distance'],1.2);self.assertLessEqual(n['conversation']['distance'],2.2)
    def test_all_required_roads_are_at_least_two_metres(self):
        ids={p['id']for p in self.layout['pathInfo']};self.assertTrue({'plaza','smith','shop','guild','dungeon','training','bounty','watch','house_chief','east','warp'}.issubset(ids));self.assertGreaterEqual(self.report['minimumRoadWidth'],2)
    def test_room_walls_leave_a_real_open_front(self):
        poly=shell(5.6,6.8,2.6);self.assertFalse(inside([0,3.39],poly));self.assertFalse(inside([0,0],poly));self.assertTrue(inside([2.75,0],poly));self.assertTrue(inside([0,-3.35],poly))
    def test_yaw_uses_unity_forward_plus_z(self):self.assertEqual(transform([1,2],[10,20],90),[12.,19.])
    def test_landmark_and_housing_are_non_enterable(self):
        bs={b['id']:b for b in self.layout['buildings']};self.assertFalse(bs['landmark']['enterable']);self.assertEqual(len([b for b in bs if b in ['house_a','house_b','house_c']]),3)

class TownApi(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=pathlib.Path(self.temp.name);p=self.root/'source';p.mkdir();textures={'backdropAlbedo':'back.png','ground':'ground.png'}
        for n in ['backdrop_rock_a.fbx','backdrop_rock_b.fbx','backdrop_rock_c.fbx','backdrop_rock_d.fbx','back.png','ground.png']:(p/n).write_bytes(b'fixture')
        self.kit={'version':6,'styleId':'anime_cel_volcano_veins_v3','textures':textures};api.write(p/'kit.json',self.kit)
        for n in ['ref.png','stone.png']:(self.root/n).write_bytes(b'\x89PNG\r\n\x1a\n'+b'\0'*8+struct.pack('>II',1024,1024))
        self.req={'apiVersion':1,'action':'validate','config':{'sourceKit':'source','materialReference':'ref.png','stoneSource':'stone.png'}}
    def tearDown(self):self.temp.cleanup()
    def test_capabilities_is_additive_and_explicit(self):
        r=api.respond({'apiVersion':1,'action':'capabilities'});self.assertTrue(r['ok']);self.assertFalse(r['result']['coreApiChanged']);self.assertEqual(r['result']['constraints']['townTriangleLimit'],60000)
    def test_wrong_style_source_rejected(self):
        self.kit['version']=5;api.write(self.root/'source/kit.json',self.kit);r=api.respond(self.req,self.root);self.assertFalse(r['ok']);self.assertEqual(r['error']['code'],'invalid_config')
    def test_existing_output_never_overwritten(self):
        self.req.update(action='generate',outputFolder='source');before=(self.root/'source/kit.json').read_bytes();r=api.respond(self.req,self.root);self.assertEqual(r['error']['code'],'output_exists');self.assertEqual((self.root/'source/kit.json').read_bytes(),before)
    def test_invalid_quality_rejected(self):
        self.req['config']['quality']='ultra';self.assertFalse(api.respond(self.req,self.root)['ok'])
    def test_invalid_version_rejected(self):self.assertFalse(api.respond({'apiVersion':2,'action':'capabilities'})['ok'])
if __name__=='__main__':unittest.main(verbosity=2)
