//! Optional native helper. HamTool's WPF interface works without this library.
use std::ffi::{c_char,CStr};
#[no_mangle]
pub unsafe extern "C" fn hamtool_validate_query(query:*const c_char)->i32 { if query.is_null(){return 0;} match CStr::from_ptr(query).to_str(){Ok(s) if !s.trim().is_empty() && s.len()<=1200=>1,_=>0} }
#[no_mangle]
pub unsafe extern "C" fn hamtool_version(out:*mut c_char,capacity:usize)->usize {let v=b"1.0.0\0";if !out.is_null()&&capacity>=v.len(){std::ptr::copy_nonoverlapping(v.as_ptr(),out.cast::<u8>(),v.len());}v.len()}
