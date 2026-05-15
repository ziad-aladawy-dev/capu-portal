import {
  GraduationCap,
  Building2,
  BookOpen,
  Users,
  CalendarDays,
  FileText,
  BadgeHelp,
  MonitorSmartphone,
  Library,
  MapPin,
  Cpu,
  Briefcase,
  HeartPulse,
  FlaskConical,
} from "lucide-react";

export const slides = [
  {
    image: "/images/University1.png",
    subtitle: "Welcome to Capital University",
    title: "A Modern University Experience",
    description:
      "Discover a university environment built for learning, innovation, research, and student success.",
    buttonText: "Get Started",
  },
  {
    image: "/images/University2.png",
    subtitle: "Academic Excellence",
    title: "Faculties, Programs, and Opportunities",
    description:
      "Explore diverse faculties, modern academic programs, and a future-ready educational journey.",
    buttonText: "Get Started",
  },
  {
    image: "/images/University3.png",
    subtitle: "Student Life & Services",
    title: "Everything Students Need in One Place",
    description:
      "Access academic support, digital services, university updates, campus resources, and more.",
    buttonText: "Get Started",
  },
];

export const faculties = [
  {
    title: "Faculty of Computer Science",
    icon: Cpu,
    desc: "Programs focused on software, artificial intelligence, data, and modern digital systems.",
  },
  {
    title: "Faculty of Engineering",
    icon: Building2,
    desc: "Practical and theoretical education across multiple engineering disciplines.",
  },
  {
    title: "Faculty of Business",
    icon: Briefcase,
    desc: "Preparing future professionals in business, management, accounting, and finance.",
  },
  {
    title: "Faculty of Pharmacy",
    icon: HeartPulse,
    desc: "Supporting healthcare education through science, knowledge, and professional practice.",
  },
  {
    title: "Faculty of Science",
    icon: FlaskConical,
    desc: "Building strong scientific foundations through research, labs, and discovery.",
  },
  {
    title: "Faculty of Arts",
    icon: BookOpen,
    desc: "Encouraging creativity, communication, culture, and critical thinking.",
  },
];

export const services = [
  { title: "Academic Calendar", icon: CalendarDays },
  { title: "Forms & Documents", icon: FileText },
  { title: "Student Guide", icon: BadgeHelp },
  { title: "E-Learning", icon: MonitorSmartphone },
  { title: "Library", icon: Library },
  { title: "Campus Services", icon: MapPin },
];

export const stats = [
  { label: "Students", value: 18000, suffix: "+", icon: GraduationCap },
  { label: "Faculties", value: 12, suffix: "", icon: Building2 },
  { label: "Programs", value: 60, suffix: "+", icon: BookOpen },
  { label: "Staff Members", value: 1200, suffix: "+", icon: Users },
];

export const news = [
  {
    title: "Admission for the new academic year is now open",
    desc: "Applications are now available for undergraduate and postgraduate programs.",
    date: "Latest Update",
  },
  {
    title: "New digital services launched for students",
    desc: "Students can now access more academic resources and online support services.",
    date: "University News",
  },
  {
    title: "Upcoming campus activities and events",
    desc: "Stay updated with workshops, seminars, and student engagement activities.",
    date: "Events",
  },
];